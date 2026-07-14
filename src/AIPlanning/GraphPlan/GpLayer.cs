using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpLayer {
        public readonly int Level;
        public GpBeliefState BeliefState { get; }
        public readonly GpActionSet ActionSet;

        public GpLayer(int level, GpBeliefState beliefState, GpActionSet actionSet) {
            Level = level;
            BeliefState = beliefState;
            ActionSet = actionSet;
        }

        public GpLayer(int level) : this(level, new GpBeliefState(), new GpActionSet()) {
        }

        public void TryAdd(GpNode gpNode) {
            switch (gpNode) {
                case GpLiteralNode stateNode:
                    BeliefState.TryAdd(stateNode);
                    break;
                case GpActionNode actionNode:
                    ActionSet.TryAdd(actionNode);
                    break;
            }
        }

        public List<GpAction> GetUsableActions(OperatorGraph operatorGraph) {
            var usableActions = new HashSet<GpAction>();
            foreach (var node in BeliefState.GetLiteralNodes) {
                foreach (var action in operatorGraph.GetActionsForLiteral(node.Literal)) {
                    usableActions.Add(action);
                }
            }

            foreach (var action in operatorGraph.GetActionsWithoutPreconditions()) {
                usableActions.Add(action);
            }

            return usableActions.ToList();
        }

        public void ExpandActions(List<GpAction> actions, Dictionary<ISentence, GpAction> persistCache) {
            foreach (var action in actions) {
                if (!action.IsApplicableToPreconditions(BeliefState, out var satisfiedPreCons)) {
                    continue;
                }

                // Connect to the canonical action node returned by Add — this avoids
                // the edge-direction asymmetry that occurred when ConnectTo ran before Add.
                var canonical = ActionSet.Add(new GpActionNode(action));
                foreach (var preCon in satisfiedPreCons) {
                    preCon.ConnectTo(canonical);
                }
            }

            foreach (var stateNode in BeliefState.GetLiteralNodes) {
                var literal = stateNode.Literal;
                if (!persistCache.TryGetValue(literal, out var persistAction)) {
                    persistAction = new GpAction("Persist",
                        new List<ISentence> { literal },
                        new List<ISentence> { literal });
                    persistCache[literal] = persistAction;
                }

                var canonical = ActionSet.Add(new GpActionNode(persistAction, true));
                stateNode.ConnectTo(canonical);
            }
        }

        public GpLayer ExpandLayer() {
            return new GpLayer(Level + 1, ActionSet.ExpandBeliefState(), new GpActionSet());
        }

        public override string ToString() {
            var output = $"Layer: {Level}\n";
            output = BeliefState.GetLiteralNodes.Aggregate(output, (current, stateNode) => current + $"{stateNode}\n");
            output += "\n";
            output = ActionSet.GetActionNodes.Aggregate(output, (current, actionNode) => current + $"{actionNode}\n");
            output += "\n";
            return output;
        }
    }
}
