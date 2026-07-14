using System.Collections.Generic;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public class GpActionSet : GpNodeSet<GpActionNode> {
        public GpActionSet() {
        }

        public GpActionSet(IEnumerable<GpNode> actionNodes) : base(actionNodes) {
        }

        public List<GpAction> GetActions(bool ignorePersistence = true) => Nodes
            .Where(n => !ignorePersistence || !n.IsPersistenceAction)
            .Select(n => n.GpAction).ToList();

        // Null when the joint preconditions contain a mutex pair: per Blum/Furst the action set
        // is infeasible at this layer, and the caller MUST try another set. Filtering the mutex
        // literals out instead would let extraction recurse with an under-constrained sub-goal.
        public GpBeliefState? GetJointPreconditionsIfConflictFree() {
            var incomingLitNodes = Nodes.SelectMany(node => node.InEdges).Distinct().ToList();
            if (!incomingLitNodes.IsConflictFree()) {
                return null;
            }
            return new GpBeliefState(incomingLitNodes);
        }

        public GpBeliefState ExpandBeliefState() {
            var beliefState = new GpBeliefState();

            foreach (var actionNode in Nodes) {
                foreach (var effect in actionNode.GpAction.Effects) {
                    var canonical = beliefState.Add(new GpLiteralNode(effect));
                    actionNode.ConnectTo(canonical);
                }
            }

            beliefState.Nodes.CheckMutexRelations();
            return beliefState;
        }

        public override string ToString() {
            return Nodes.Aggregate("ActionSet:\n", (current, node) => current + $"\t{node}\n");
        }
    }
}
