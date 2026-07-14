using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class OperatorGraph
    {
        private readonly GpProblem _problem;
        private readonly List<GpLiteralNode> _literalNodes = new();
        private readonly List<GpAction> _actions;
        private readonly List<GpAction> _preconditionlessInstances = new();

        // Caps how often one action node is revisited while the graph is built backwards from
        // Finish; without it, mutually-supporting actions (A enables B enables A ...) would
        // recurse forever. The graph only needs each literal→action edge once, so a small
        // constant bound loses nothing.
        private const int UseCountStop = 10;

        public OperatorGraph(GpProblem problem)
        {
            _problem = problem;
            // Work on clones: graph construction accumulates grounding state on the actions
            // (AddUnificators) and Init injects the synthetic Start/Finish actions. None of that
            // may leak into the caller's problem — a GpProblem must stay reusable across solves.
            _actions = problem.Actions.Select(action => action.Clone()).ToList();
            Init();
        }

        private void Init()
        {
            var startNode = new GpActionNode(new GpAction("Start",
                new List<ISentence>(), new List<ISentence>(_problem.InitialState), isSynthetic: true));
            var finishNode = new GpActionNode(new GpAction("Finish",
                new List<ISentence>(_problem.Goals), new List<ISentence>(), isSynthetic: true));

            //init the effects of start as preconditions
            foreach (var preCon in startNode.GpAction.Effects)
            {
                _literalNodes.Add(new GpLiteralNode(preCon));
            }

            _actions.Add(startNode.GpAction);
            _actions.Add(finishNode.GpAction);

            ConstructGraphRecursivly(finishNode);
            ReplaceAbstractWithConcreteActions();
        }

        public List<GpAction> GetActionsForLiteral(ISentence literal)
        {
            // Unification-based lookup: graph nodes created from non-ground preconditions
            // (e.g. Q(x)) must be found by the ground literals (Q(a)) that arise at runtime;
            // exact equality would leave every action anchored only to such a node unreachable.
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

                    // The synthetic Start/Finish actions only bootstrap the backward graph
                    // construction; their instances must not surface at runtime (a Finish
                    // hanging off the goal literals would enter every layer's action set and
                    // mutex computation once the goals appear in a belief state).
                    if (!actionNode.GpAction.IsSynthetic)
                    {
                        allInstances.AddRange(instances);
                    }

                    literalNode.OutEdges.Remove(actionNode);
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
                var possibleInstances = new List<GpAction>();

                var noMultipleInstancesNeeded = action.Unificators.All(u => u.IsEmpty);
                if (noMultipleInstancesNeeded)
                {
                    var clone = action.Clone();
                    // Non-ground instances can never fire (belief states hold only ground
                    // literals, matched exactly) — dead weight in every layer's scans.
                    if (clone.IsGround() || action.IsSynthetic)
                    {
                        possibleInstances.Add(clone);
                    }
                    mapping.Add(action, possibleInstances);
                    continue;
                }

                var conflictFreeUnificators = action.GetConflictFreeUnificatorPossibilities(action.Unificators);
                foreach (var unificator in conflictFreeUnificators)
                {
                    var clone = action.Clone();
                    clone.SpecifyAction(unificator);
                    if (clone.IsConsistent() && clone.IsGround())
                    {
                        possibleInstances.Add(clone);
                    }
                }

                mapping.Add(action, possibleInstances);
            }

            return mapping;
        }

        private void ConstructGraphRecursivly(GpNode curNode)
        {
            var operatorNodes = new Dictionary<GpAction, GpActionNode>();

            switch (curNode)
            {
                case GpActionNode curOperator:
                    MapPreConditionsToAction(curOperator, operatorNodes);
                    break;
                case GpLiteralNode curState:
                    FindApplicableAction(curState, operatorNodes);
                    break;
            }
        }

        private void MapPreConditionsToAction(GpActionNode curAction, Dictionary<GpAction, GpActionNode> operatorNodes)
        {
            //necessary preconditions of an action but not sufficient

            foreach (var preCon in curAction.GpAction.Preconditions)
            {
                if (!TryGetMatchingLiteralNodes(preCon, out var literalNodes, out var unificators))
                {
                    // Non-ground literal nodes (e.g. Q(x)) are fine here: they act as unification
                    // anchors — GetActionsForLiteral matches ground runtime literals against them
                    // via Match, and InstantiateActions later drops any action INSTANCE that keeps
                    // an unbound variable, so no non-ground literal ever reaches a belief state.
                    literalNodes = new List<GpLiteralNode>() { new(preCon) };
                    _literalNodes.AddRange(literalNodes);
                }

                curAction.GpAction.AddUnificators(unificators);

                foreach (var literalNode in literalNodes)
                {
                    literalNode.ConnectTo(curAction);
                    FindApplicableAction(literalNode, operatorNodes);
                }
            }
        }

        private bool TryGetMatchingLiteralNodes(ISentence literal, out List<GpLiteralNode> preConNodes, out List<Unificator> unificators)
        {
            var isMatch = false;
            unificators = new List<Unificator>();
            preConNodes = new List<GpLiteralNode>();

            foreach (var node in _literalNodes)
            {
                if (!node.Literal.Match(literal, out var uni))
                {
                    continue;
                }

                preConNodes.Add(node);
                unificators.Add(uni);
                isMatch = true;
            }

            return isMatch;
        }

        private void FindApplicableAction(GpLiteralNode curLiteral, Dictionary<GpAction, GpActionNode> operatorNodes)
        {
            foreach (var action in _actions)
            {
                if (!IsEffectsApplicable(action, curLiteral))
                {
                    continue;
                }

                if (!operatorNodes.TryGetValue(action, out var operatorNode))
                {
                    operatorNode = new GpActionNode(action);
                    operatorNodes.Add(action, operatorNode);
                }
                else if (!operatorNode.TryIncreaseUseCount(UseCountStop))
                {
                    continue;
                }

                operatorNode.ConnectTo(curLiteral);
                MapPreConditionsToAction(operatorNode, operatorNodes);
            }
        }

        private bool IsEffectsApplicable(GpAction action, GpLiteralNode literalNode)
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
                // PRODUCER, while bindings for the node literal's variables ground the CONSUMERS
                // anchored on this node (e.g. effect Have(Bread) vs precondition anchor Have(x)
                // binds the consumer's x). Routing everything to the producer would leave a
                // precondition-only variable unbound forever, and every consumer instance would
                // be dropped as non-ground.
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