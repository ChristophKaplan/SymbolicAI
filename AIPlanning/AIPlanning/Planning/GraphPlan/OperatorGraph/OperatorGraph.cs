using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class OperatorGraph
    {
        private readonly GpProblem _problem;
        private readonly List<GpLiteralNode> _literalNodes = new();
        private readonly List<GpAction> _actions;

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
                new List<ISentence>(), new List<ISentence>(_problem.InitialState)));
            var finishNode = new GpActionNode(new GpAction("Finish",
                new List<ISentence>(_problem.Goals), new List<ISentence>()));

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
            var instances = new List<GpAction>();
            var node = _literalNodes.FirstOrDefault(node => literal.Equals(node.Literal));
            if (node == null)
            {
                return instances;
            }

            var direct = node.OutEdges.Select(outEdge => ((GpActionNode)outEdge).GpAction).ToList();
            instances.AddRange(direct);

            return instances;
        }

        private void ReplaceAbstractWithConcreteActions()
        {
            var instanceMap = InstantiateActions();

            foreach (var literalNode in _literalNodes)
            {
                var allInstances = new List<GpAction>();
                for (var i = literalNode.OutEdges.Count - 1; i >= 0; i--)
                {
                    var outEdge = literalNode.OutEdges[i];
                    if (outEdge is GpActionNode actionNode &&
                        instanceMap.TryGetValue(actionNode.GpAction, out var instances))
                    {
                        allInstances.AddRange(instances);
                        literalNode.OutEdges.Remove(actionNode);
                    }
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
                    possibleInstances.Add(action.Clone());
                    mapping.Add(action, possibleInstances);
                    continue;
                }

                var conflictFreeUnificators = action.GetConflictFreeUnificatorPossibilities(action.Unificators);
                foreach (var unificator in conflictFreeUnificators)
                {
                    var clone = action.Clone();
                    clone.SpecifyAction(unificator);
                    if (clone.IsConsistent())
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
                    //TODO: clarify if its a problem that we can add literals with "unspecified variables" here.
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
                if (!IsEffectsApplicable(action, curLiteral.Literal))
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

        private bool IsEffectsApplicable(GpAction action, ISentence literal)
        {
            var uniList = new List<Unificator>();
            var isMatch = false;

            foreach (var effect in action.Effects)
            {
                if (!effect.Match(literal, out var uni))
                {
                    continue;
                }

                if (!uni.IsEmpty)
                {
                    uniList.Add(uni);
                }

                isMatch = true;
            }

            if (!isMatch)
            {
                return false;
            }

            action.AddUnificators(uniList);
            return action.IsConsistent();
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