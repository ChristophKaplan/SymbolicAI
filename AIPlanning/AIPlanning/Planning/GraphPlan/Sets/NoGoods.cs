using System.Collections.Generic;

namespace AIPlanning.Planning.GraphPlan {
    // Per-level set of belief states that have been proven unreachable.
    // Used by FindSolutions to prune already-failed sub-goals and to detect
    // graph saturation (no new nogood added across two consecutive expansions).
    public class NoGoods {
        private readonly Dictionary<int, HashSet<GpBeliefState>> _byLevel = new();
        private int _sizeAtLastExpansion = -1;
        private int _sizeAtPreviousExpansion = -2;

        public void Add(int level, GpBeliefState subGoalState) {
            if (!_byLevel.TryGetValue(level, out var set)) {
                set = new HashSet<GpBeliefState>();
                _byLevel[level] = set;
            }

            set.Add(subGoalState);
        }

        public bool Contains(int level, GpBeliefState state) {
            return _byLevel.TryGetValue(level, out var set) && set.Contains(state);
        }

        // Snapshot the total nogood count. Called once per graph-expansion step so
        // that IsStable can detect "no new nogoods added in the last cycle".
        public void MarkExpansion() {
            _sizeAtPreviousExpansion = _sizeAtLastExpansion;
            _sizeAtLastExpansion = TotalCount();
        }

        // Stable iff the nogood set didn't grow between the two most recent
        // expansion checkpoints, i.e. the algorithm cannot make further progress.
        public bool IsStable() {
            return _sizeAtLastExpansion >= 0
                && _sizeAtPreviousExpansion == _sizeAtLastExpansion;
        }

        private int TotalCount() {
            var sum = 0;
            foreach (var kvp in _byLevel) {
                sum += kvp.Value.Count;
            }
            return sum;
        }
    }
}
