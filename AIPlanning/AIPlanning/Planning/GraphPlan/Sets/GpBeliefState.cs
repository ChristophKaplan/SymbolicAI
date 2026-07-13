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

        // Same literals AND the same mutex relations between them. This is the fixed-point
        // notion level-off detection needs: literal sets stabilise before mutexes do.
        public bool EqualState(GpBeliefState other) {
            if (!EqualStateLiterals(other)) {
                return false;
            }

            for (var i = 0; i < _literalNodes.Count; i++) {
                for (var j = i + 1; j < _literalNodes.Count; j++) {
                    var a = _literalNodes[i];
                    var b = _literalNodes[j];
                    var otherA = other._literalNodes.First(a.EqualLiteral);
                    var otherB = other._literalNodes.First(b.EqualLiteral);
                    if (AreMutex(a, b) != AreMutex(otherA, otherB)) {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool IsConflictFreeStateReachable(List<ISentence> literals, out GpBeliefState? conflictFreeState) {
            // Duplicate goal literals map onto one node; comparing against the raw count would
            // make such goal lists unsatisfiable forever.
            var distinct = literals.Distinct().ToList();
            var reachedSubState = GetSubSetOfNodesMatching(distinct);

            if (reachedSubState == null) {
                conflictFreeState = null;
                return false;
            }

            var conflictFree = reachedSubState.GetConflictFreeSubset();
            conflictFreeState = new GpBeliefState(conflictFree);
            return conflictFree.Count == distinct.Count;
        }

        // Blum & Furst §3.2: choose one supporting action per goal literal, never picking a
        // supporter mutex with one already selected, and reusing a selection that also supports
        // a later goal (minimality). This backtracking DFS prunes mutex partial selections as it
        // goes, instead of materialising the full cartesian product of every goal's supporters
        // and filtering afterwards — the eager product blew up exponentially with the number of
        // supporters per goal (e.g. one Chop grounding per tree).
        public IEnumerable<GpActionSet> GetPossibleConflictFreeActionSets() {
            return SelectSupporters(0, new List<GpNode>());
        }

        private IEnumerable<GpActionSet> SelectSupporters(int goalIndex, List<GpNode> chosen) {
            if (goalIndex == _literalNodes.Count) {
                if (chosen.Count > 0) {
                    yield return new GpActionSet(new List<GpNode>(chosen));
                }
                yield break;
            }

            var goalSupporters = _literalNodes[goalIndex].InEdges;

            // Minimality: if something already selected supports this goal, reuse it rather than
            // branching on every supporter. This is what stops redundant supersets — a real
            // action plus the Persist of the same literal — from multiplying out.
            if (chosen.Any(goalSupporters.Contains)) {
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                yield break;
            }

            foreach (var supporter in goalSupporters) {
                if (chosen.Any(c => AreMutex(c, supporter))) {
                    continue;
                }

                chosen.Add(supporter);
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        // Mutex is recorded symmetrically (GpNode.TryAddMutexRelations), so checking one side suffices.
        private static bool AreMutex(GpNode a, GpNode b) {
            return a.MutexRelation.Any(m => m.ToNode.Equals(b));
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
