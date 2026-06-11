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

        // Returns the canonical action node stored in the set (existing or just-added).
        public GpActionNode Add(GpActionNode actionNode) {
            var contained = _actionNodes.FirstOrDefault(actionNode.Equals);
            if (contained != null) {
                return contained;
            }

            _actionNodes.Add(actionNode);
            return actionNode;
        }

        // Backward-compatible void overload.
        public void TryAdd(GpActionNode actionNode) => Add(actionNode);

        // Joint preconditions of every action in this set, deduplicated. Returns null
        // when those preconditions contain a mutex pair: per Blum/Furst that means
        // the action set as a whole is infeasible at this layer, and the caller MUST
        // try another action set rather than silently dropping the offending literals.
        // (The previous implementation filtered mutex literals out, which let the
        //  backward-extraction recurse with an under-constrained sub-goal — visible as
        //  "step 2: Work" with two of its preconditions never produced upstream.)
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

            if (_actionNodes.Count != other._actionNodes.Count) {
                return false;
            }

            foreach (var node in _actionNodes) {
                if (!other._actionNodes.Contains(node)) {
                    return false;
                }
            }

            return true;
        }

        public override string ToString() {
            return _actionNodes.Aggregate("ActionSet:\n", (current, node) => current + $"\t{node}\n");
        }
    }
}
