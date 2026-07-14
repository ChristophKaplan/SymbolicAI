using System.Collections.Generic;
using System.Linq;

namespace AIPlanning.Planning.GraphPlan {
    public class GpActionSet {
        private readonly List<GpActionNode> _actionNodes = new();

        public IReadOnlyList<GpNode> GetNodes => _actionNodes;
        public List<GpActionNode> GetActionNodes => _actionNodes;

        public List<GpAction> GetActions(bool ignorePersistence = true) => _actionNodes
            .Where(n => !ignorePersistence || !n.IsPersistenceAction)
            .Select(n => n.GpAction).ToList();

        public GpActionSet() {
        }

        public GpActionSet(IEnumerable<GpNode> actionNodes) {
            _actionNodes = actionNodes.Select(n => (GpActionNode)n).ToList();
        }

        // Returns the canonical node stored in the set; callers MUST connect edges to the
        // returned instance, not to their input.
        public GpActionNode Add(GpActionNode actionNode) {
            var contained = _actionNodes.FirstOrDefault(actionNode.Equals);
            if (contained != null) {
                return contained;
            }

            _actionNodes.Add(actionNode);
            return actionNode;
        }

        public void TryAdd(GpActionNode actionNode) => Add(actionNode);

        // Null when the joint preconditions contain a mutex pair: per Blum/Furst the action set
        // is infeasible at this layer, and the caller MUST try another set. Filtering the mutex
        // literals out instead would let extraction recurse with an under-constrained sub-goal.
        public GpBeliefState? GetJointPreconditionsIfConflictFree() {
            var incomingLitNodes = _actionNodes.SelectMany(node => node.InEdges).Distinct().ToList();
            if (!incomingLitNodes.IsConflictFree()) {
                return null;
            }
            return new GpBeliefState(incomingLitNodes);
        }

        public GpBeliefState ExpandBeliefState() {
            var beliefState = new GpBeliefState();

            foreach (var actionNode in _actionNodes) {
                foreach (var effect in actionNode.GpAction.Effects) {
                    var canonical = beliefState.Add(new GpLiteralNode(effect));
                    actionNode.ConnectTo(canonical);
                }
            }

            beliefState.GetNodes.CheckMutexRelations();
            return beliefState;
        }

        public override int GetHashCode() {
            var hash = _actionNodes.Count;
            foreach (var actionNode in _actionNodes) {
                hash ^= actionNode.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj is not GpActionSet other) {
                return false;
            }

            return _actionNodes.MultisetEquals(other._actionNodes);
        }

        public override string ToString() {
            return _actionNodes.Aggregate("ActionSet:\n", (current, node) => current + $"\t{node}\n");
        }
    }
}
