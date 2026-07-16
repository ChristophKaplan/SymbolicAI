using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class OperatorGraph
    {
        private readonly List<GpLiteralNode> _literalNodes = new();
        private readonly List<GpAction> _actions;
        private readonly List<GpAction> _preconditionlessInstances = new();

        // Caps revisits of one action node during the backward construction; mutually-supporting
        // actions (A enables B enables A ...) would otherwise recurse forever.
        private const int MaxUseCount = 10;

        public OperatorGraph(GpProblem problem)
        {
            // Clones: construction accumulates grounding state on the actions and injects
            // Start/Finish; none of that may leak — a GpProblem must stay reusable across solves.
            // Content-equal duplicates would collide as dictionary keys during grounding.
            _actions = problem.Actions.Distinct().Select(action => action.Clone()).ToList();
            Init(problem);
        }

        private void Init(GpProblem problem)
        {
            var startNode = new GpActionNode(new GpAction("Start",
                new List<ISentence>(), new List<ISentence>(problem.InitialState), isSynthetic: true));
            var finishNode = new GpActionNode(new GpAction("Finish",
                new List<ISentence>(problem.Goals), new List<ISentence>(), isSynthetic: true));

            foreach (var preCon in startNode.GpAction.Effects)
            {
                _literalNodes.Add(new GpLiteralNode(preCon));
            }

            _actions.Add(startNode.GpAction);
            _actions.Add(finishNode.GpAction);

            MapPreConditionsToAction(finishNode, new Dictionary<GpAction, GpActionNode>());
            ReplaceAbstractWithConcreteActions();
        }

        public List<GpAction> GetActionsForLiteral(ISentence literal)
        {
            // Unification-based lookup: nodes created from non-ground preconditions (Q(x)) must be
            // found by the ground literals (Q(a)) arising at runtime; exact equality would leave
            // every action anchored only to such a node unreachable.
            var instances = new List<GpAction>();
            foreach (var node in _literalNodes)
            {
                if (!node.Literal.Match(literal, out _))
                {
                    continue;
                }

                foreach (var outEdge in node.OutEdges)
                {
                    instances.Add(((GpActionNode)outEdge).GpAction);
                }
            }

            return instances;
        }

        // Actions without preconditions hang off no literal node, so the edge-driven lookup
        // above can never surface them; they are applicable in any state.
        public IReadOnlyList<GpAction> GetActionsWithoutPreconditions() => _preconditionlessInstances;

        private void ReplaceAbstractWithConcreteActions()
        {
            var instanceMap = InstantiateActions();

            foreach (var pair in instanceMap)
            {
                if (pair.Key.Preconditions.Count == 0 && !pair.Key.IsSynthetic)
                {
                    _preconditionlessInstances.AddRange(pair.Value);
                }
            }

            foreach (var literalNode in _literalNodes)
            {
                var allInstances = new List<GpAction>();
                for (var i = literalNode.OutEdges.Count - 1; i >= 0; i--)
                {
                    var outEdge = literalNode.OutEdges[i];
                    if (outEdge is not GpActionNode actionNode ||
                        !instanceMap.TryGetValue(actionNode.GpAction, out var instances))
                    {
                        continue;
                    }

                    // Start/Finish only bootstrap the backward construction; a Finish hanging off
                    // the goal literals would enter every layer's action set at runtime.
                    if (!actionNode.GpAction.IsSynthetic)
                    {
                        allInstances.AddRange(instances);
                    }

                    literalNode.DisconnectFrom(actionNode);
                }

                foreach (var instance in allInstances)
                {
                    literalNode.ConnectTo(new GpActionNode(instance));
                }
            }
        }

        private Dictionary<GpAction, List<GpAction>> InstantiateActions()
        {
            var mapping = new Dictionary<GpAction, List<GpAction>>();
            foreach (var action in _actions)
            {
                mapping.Add(action, InstantiateAction(action));
            }

            PropagateGroundEffectBindings(mapping);
            return mapping;
        }

        private static List<GpAction> InstantiateAction(GpAction action)
        {
            var possibleInstances = new List<GpAction>();

            var noMultipleInstancesNeeded = action.Unificators.All(u => u.IsEmpty);
            if (noMultipleInstancesNeeded)
            {
                var clone = action.Clone();
                // Non-ground instances can never fire (belief states hold only ground
                // literals, matched exactly) — dead weight in every layer's scans.
                if (action.IsSynthetic || (clone.IsGround() && clone.IsConsistent()))
                {
                    possibleInstances.Add(clone);
                }
                return possibleInstances;
            }

            foreach (var unificator in action.GetConflictFreeUnifiers())
            {
                var instance = action.Substitute(unificator);
                if (instance.IsConsistent() && instance.IsGround())
                {
                    possibleInstances.Add(instance);
                }
            }

            return possibleInstances;
        }

        // Variable-to-variable producer/consumer matches (effect Q(w) vs anchor Q(y)) yield no
        // constant binding during construction; only once the producer is GROUND-instantiated does
        // its effect Q(K) reveal the consumer binding y→K. Re-matching ground effects against the
        // non-ground anchor nodes and re-instantiating the consumers repeats until no new binding
        // appears — each round can unlock the next link of a producer chain.
        private void PropagateGroundEffectBindings(Dictionary<GpAction, List<GpAction>> mapping)
        {
            bool learnedNewBinding;
            do
            {
                learnedNewBinding = false;

                foreach (var literalNode in _literalNodes)
                {
                    if (literalNode.Literal.IsGround())
                    {
                        continue;
                    }

                    var bindings = new List<Unificator>();
                    foreach (var instances in mapping.Values)
                    {
                        foreach (var instance in instances)
                        {
                            foreach (var effect in instance.Effects)
                            {
                                if (effect.Match(literalNode.Literal, out var uni) && !uni.IsEmpty)
                                {
                                    bindings.Add(uni);
                                }
                            }
                        }
                    }

                    if (bindings.Count == 0)
                    {
                        continue;
                    }

                    foreach (var outEdge in literalNode.OutEdges)
                    {
                        var consumer = ((GpActionNode)outEdge).GpAction;
                        if (consumer.AddUnificators(bindings))
                        {
                            mapping[consumer] = InstantiateAction(consumer);
                            learnedNewBinding = true;
                        }
                    }
                }
            } while (learnedNewBinding);
        }

        private void MapPreConditionsToAction(GpActionNode curAction, Dictionary<GpAction, GpActionNode> operatorNodes)
        {
            foreach (var preCon in curAction.GpAction.Preconditions)
            {
                GetMatchingLiteralNodes(preCon, out var literalNodes, out var unificators);

                // A merely unifying node is not enough: a more specific existing node (Q(Home))
                // matches a general precondition (Q(y)) without covering it, making solvability
                // depend on action declaration order — so the precondition gets its own anchor
                // node unless an equal one exists. Non-ground anchors are safe: InstantiateActions
                // drops any instance that keeps an unbound variable.
                if (literalNodes.All(node => !node.Literal.Equals(preCon)))
                {
                    var anchor = new GpLiteralNode(preCon);
                    literalNodes.Add(anchor);
                    _literalNodes.Add(anchor);
                }

                curAction.GpAction.AddUnificators(unificators);

                foreach (var literalNode in literalNodes)
                {
                    literalNode.ConnectTo(curAction);
                    ConnectApplicableActions(literalNode, operatorNodes);
                }
            }
        }

        private void GetMatchingLiteralNodes(ISentence literal, out List<GpLiteralNode> matchingLiteralNodes, out List<Unificator> unificators)
        {
            unificators = new List<Unificator>();
            matchingLiteralNodes = new List<GpLiteralNode>();

            foreach (var node in _literalNodes)
            {
                if (!node.Literal.Match(literal, out var uni))
                {
                    continue;
                }

                matchingLiteralNodes.Add(node);
                unificators.Add(uni);
            }
        }

        private void ConnectApplicableActions(GpLiteralNode curLiteral, Dictionary<GpAction, GpActionNode> operatorNodes)
        {
            foreach (var action in _actions)
            {
                if (!TryBindEffects(action, curLiteral))
                {
                    continue;
                }

                if (!operatorNodes.TryGetValue(action, out var operatorNode))
                {
                    operatorNode = new GpActionNode(action);
                    operatorNodes.Add(action, operatorNode);
                }
                else if (!operatorNode.TryIncreaseUseCount(MaxUseCount))
                {
                    continue;
                }

                operatorNode.ConnectTo(curLiteral);
                MapPreConditionsToAction(operatorNode, operatorNodes);
            }
        }

        private bool TryBindEffects(GpAction action, GpLiteralNode literalNode)
        {
            var literal = literalNode.Literal;
            var producerBindings = new List<Unificator>();
            var consumerBindings = new List<Unificator>();
            var isMatch = false;

            foreach (var effect in action.Effects)
            {
                if (!effect.Match(literal, out var uni))
                {
                    continue;
                }

                isMatch = true;
                if (uni.IsEmpty)
                {
                    continue;
                }

                // The unifier mixes two owners: bindings for the effect's variables ground the
                // PRODUCER, bindings for the node literal's variables ground the CONSUMERS anchored
                // on this node. Routing everything to the producer would leave a precondition-only
                // variable unbound forever, and every consumer instance dropped as non-ground.
                var literalVariables = new HashSet<Variable>(LiteralVariables(literal));
                var producer = new Dictionary<Variable, Term>();
                var consumer = new Dictionary<Variable, Term>();
                foreach (var (variable, term) in uni.Substitutions)
                {
                    if (literalVariables.Contains(variable))
                    {
                        consumer.Add(variable, term);
                    }
                    else
                    {
                        producer.Add(variable, term);
                    }
                }

                if (producer.Count > 0)
                {
                    producerBindings.Add(new Unificator(producer));
                }
                if (consumer.Count > 0)
                {
                    consumerBindings.Add(new Unificator(consumer));
                }
            }

            if (!isMatch)
            {
                return false;
            }

            action.AddUnificators(producerBindings);

            if (consumerBindings.Count > 0)
            {
                foreach (var outEdge in literalNode.OutEdges)
                {
                    if (outEdge is GpActionNode consumerNode)
                    {
                        consumerNode.GpAction.AddUnificators(consumerBindings);
                    }
                }
            }

            return action.IsConsistent();
        }

        private static IEnumerable<Variable> LiteralVariables(ISentence literal)
        {
            var atom = literal is IComplexSentence complex ? complex.Children[0] : literal;
            return atom is IPredicate predicate ? predicate.GetVariables() : Enumerable.Empty<Variable>();
        }

        public override string ToString()
        {
            var output = "Operator Graph\n";

            foreach (var preConNode in _literalNodes)
            {
                var preCon = preConNode.Literal;
                var outEdges =
                    preConNode.OutEdges.Aggregate("", (acc, edge) => acc + $"\n\t\t\t\t{((GpActionNode)edge).GpAction},");
                output += $"Literal: {preCon} out:{preConNode.OutEdges.Count} -> [{outEdges}]\n";
            }

            return output;
        }
    }
}