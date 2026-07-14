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

        public IReadOnlyList<GpNode> GetNodes => _literalNodes;
        public List<GpLiteralNode> GetLiteralNodes => _literalNodes;

        // Returns the canonical node stored in the belief state; callers MUST connect edges to
        // the returned instance, not to their input.
        public GpLiteralNode Add(GpLiteralNode literalNode) {
            var contained = _literalNodes.FirstOrDefault(literalNode.Equals);
            if (contained != null) {
                return contained;
            }

            _literalNodes.Add(literalNode);
            return literalNode;
        }

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
            var aSubsetB = _literalNodes.All(a => other._literalNodes.Any(a.EqualLiteral));
            var bSubsetA = other._literalNodes.All(b => _literalNodes.Any(b.EqualLiteral));
            return aSubsetB && bSubsetA;
        }

        // Same literals AND the same mutex relations between them — the fixed-point notion
        // level-off detection needs, since literal sets stabilise before mutexes do.
        public bool EqualState(GpBeliefState other) {
            if (!EqualStateLiterals(other)) {
                return false;
            }

            // Align the node lists once; aligning inside the pair loop is O(L³).
            var aligned = new GpLiteralNode[_literalNodes.Count];
            for (var i = 0; i < _literalNodes.Count; i++) {
                aligned[i] = other._literalNodes.First(_literalNodes[i].EqualLiteral);
            }

            for (var i = 0; i < _literalNodes.Count; i++) {
                for (var j = i + 1; j < _literalNodes.Count; j++) {
                    if (_literalNodes[i].IsMutexWith(_literalNodes[j]) != aligned[i].IsMutexWith(aligned[j])) {
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
        // supporter mutex with one already selected. The backtracking DFS prunes mutex partial
        // selections as it goes; materialising the full cartesian product of every goal's
        // supporters first blows up exponentially.
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

            // Minimality: reuse an already-selected supporter rather than branching on every one;
            // this stops redundant supersets (a real action plus the same literal's Persist).
            if (chosen.Any(goalSupporters.Contains)) {
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                yield break;
            }

            foreach (var supporter in goalSupporters) {
                if (chosen.Any(c => c.IsMutexWith(supporter))) {
                    continue;
                }

                chosen.Add(supporter);
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        public override int GetHashCode() {
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

            return _literalNodes.MultisetEquals(other._literalNodes);
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
