using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpBeliefState {
        private readonly List<GpLiteralNode> _literalNodes = new();

        public GpBeliefState() {
        }

        public GpBeliefState(IEnumerable<GpNode> nodes) {
            _literalNodes = nodes.Select(n => (GpLiteralNode)n).ToList();
        }

        // Covariant cast via IReadOnlyList<T>: zero-allocation view onto the typed backing list.
        public IReadOnlyList<GpNode> GetNodes => _literalNodes;
        public List<GpLiteralNode> GetLiteralNodes => _literalNodes;

        // Returns the canonical node stored in the belief state (existing or just-added).
        // Callers MUST connect edges to the returned instance, not to their input.
        public GpLiteralNode Add(GpLiteralNode literalNode) {
            var contained = _literalNodes.FirstOrDefault(literalNode.Equals);
            if (contained != null) {
                return contained;
            }

            _literalNodes.Add(literalNode);
            return literalNode;
        }

        // Backward-compatible void overload (used by GpLayer dispatcher and Init).
        public void TryAdd(GpLiteralNode literalNode) => Add(literalNode);

        public List<GpNode>? GetSubSetOfNodesMatching(List<ISentence> literals) {
            var subset = new List<GpNode>();

            foreach (var literal in literals) {
                var applicableNode = _literalNodes.FirstOrDefault(node => node.Literal.Equals(literal));
                if (applicableNode == null) {
                    return null;
                }

                subset.Add(applicableNode);
            }

            return subset.Distinct().ToList();
        }

        public bool EqualStateLiterals(GpBeliefState other) {
            // Order-insensitive: check both subset relations.
            var aSubsetB = _literalNodes.All(a => other._literalNodes.Any(a.EqualLiteral));
            var bSubsetA = other._literalNodes.All(b => _literalNodes.Any(b.EqualLiteral));
            return aSubsetB && bSubsetA;
        }

        public bool IsConflictFreeStateReachable(List<ISentence> literals, out GpBeliefState? conflictFreeState) {
            var reachedSubState = GetSubSetOfNodesMatching(literals);

            if (reachedSubState == null) {
                conflictFreeState = null;
                return false;
            }

            var conflictFree = reachedSubState.GetConflictFreeSubset();
            conflictFreeState = new GpBeliefState(conflictFree);
            return conflictFree.Count == literals.Count;
        }

        public List<GpActionSet> GetPossibleConflictFreeActionSets() {
            var inEdgesActionLists = _literalNodes.Select(stateNode => stateNode.InEdges).ToList();
            var possibleCombinationsOfActions = inEdgesActionLists.GetCombinations().Select(c => c.Distinct().ToList()).ToList();

            var possibleActionSets = new List<GpActionSet>();

            foreach (var possibleActionNodes in possibleCombinationsOfActions) {
                if (possibleActionNodes.Count == 0) {
                    continue;
                }

                if (possibleActionNodes.IsConflictFree()) {
                    possibleActionSets.Add(new GpActionSet(possibleActionNodes));
                }
            }

            return possibleActionSets;
        }

        public override int GetHashCode() {
            // Order-insensitive: XOR of element hashes.
            var hash = _literalNodes.Count;
            foreach (var node in _literalNodes) {
                hash ^= node.GetHashCode();
            }

            return hash;
        }

        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj is not GpBeliefState other) {
                return false;
            }

            if (_literalNodes.Count != other._literalNodes.Count) {
                return false;
            }

            // Set semantics: every literal in `this` must occur in `other` and vice versa.
            // Counts are equal, so checking one direction suffices.
            foreach (var node in _literalNodes) {
                if (!other._literalNodes.Contains(node)) {
                    return false;
                }
            }

            return true;
        }

        public override string ToString() {
            var output = "BeliefState:\n";

            foreach (var node in _literalNodes) {
                output += $"\t{node}\n";
            }

            return output;
        }
    }
}
