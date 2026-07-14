using System.Collections.Generic;

namespace AIPlanning.Planning.GraphPlan {
    // Per-level set of belief states proven unreachable; prunes failed sub-goals and detects
    // search exhaustion (Blum & Furst's termination condition).
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

        // Snapshot the nogood count at the levelled-off level. A total across all levels grows on
        // every failed attempt by construction; the count at one fixed level is monotone and
        // bounded, so two equal consecutive snapshots mean the search stage added nothing.
        public void MarkExpansion(int level) {
            _sizeAtPreviousExpansion = _sizeAtLastExpansion;
            _sizeAtLastExpansion = CountAt(level);
        }

        public bool IsStable() {
            return _sizeAtLastExpansion >= 0
                && _sizeAtPreviousExpansion == _sizeAtLastExpansion;
        }

        private int CountAt(int level) {
            return _byLevel.TryGetValue(level, out var set) ? set.Count : 0;
        }
    }
}
