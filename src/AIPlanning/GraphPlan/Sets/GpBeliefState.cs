using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpBeliefState : GpNodeSet<GpLiteralNode> {
        public GpBeliefState() {
        }

        public GpBeliefState(IEnumerable<GpLiteralNode> nodes) : base(nodes) {
        }

        public List<GpLiteralNode>? GetSubSetOfNodesMatching(List<ISentence> literals) {
            var subset = new List<GpLiteralNode>();

            foreach (var literal in literals) {
                var applicableNode = Nodes.FirstOrDefault(node => node.Literal.Equals(literal));
                if (applicableNode == null) {
                    return null;
                }

                subset.Add(applicableNode);
            }

            return subset.Distinct().ToList();
        }

        public bool EqualStateLiterals(GpBeliefState other) {
            var aSubsetB = Nodes.All(a => other.Nodes.Any(a.Equals));
            var bSubsetA = other.Nodes.All(b => Nodes.Any(b.Equals));
            return aSubsetB && bSubsetA;
        }

        // Same literals AND the same mutex relations between them — the fixed-point notion
        // level-off detection needs, since literal sets stabilise before mutexes do.
        public bool EqualState(GpBeliefState other) {
            if (!EqualStateLiterals(other)) {
                return false;
            }

            // Align the node lists once; aligning inside the pair loop is O(L³).
            var aligned = new GpLiteralNode[Nodes.Count];
            for (var i = 0; i < Nodes.Count; i++) {
                aligned[i] = other.Nodes.First(Nodes[i].Equals);
            }

            for (var i = 0; i < Nodes.Count; i++) {
                for (var j = i + 1; j < Nodes.Count; j++) {
                    if (Nodes[i].IsMutexWith(Nodes[j]) != aligned[i].IsMutexWith(aligned[j])) {
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
            return SelectSupporters(0, new List<GpActionNode>());
        }

        private IEnumerable<GpActionSet> SelectSupporters(int goalIndex, List<GpActionNode> chosen) {
            if (goalIndex == Nodes.Count) {
                if (chosen.Count > 0) {
                    yield return new GpActionSet(new List<GpActionNode>(chosen));
                }
                yield break;
            }

            var goalNode = Nodes[goalIndex];

            // Minimality: reuse an already-selected supporter rather than branching on every one;
            // this stops redundant supersets (a real action plus the same literal's Persist).
            if (chosen.Any(c => c.HasOutEdge(goalNode))) {
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                yield break;
            }

            foreach (var supporter in goalNode.InEdges) {
                if (chosen.Any(c => c.IsMutexWith(supporter))) {
                    continue;
                }

                chosen.Add((GpActionNode)supporter);
                foreach (var set in SelectSupporters(goalIndex + 1, chosen)) {
                    yield return set;
                }
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        public override string ToString() {
            return Nodes.Aggregate("BeliefState:\n", (current, node) => current + $"\t{node}\n");
        }
    }
}
