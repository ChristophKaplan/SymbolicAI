using System.Collections.Generic;

namespace AIPlanning.Planning.GraphPlan {
    // Per-level set of belief states that have been proven unreachable.
    // Used by FindSolutions to prune already-failed sub-goals and to detect
    // search exhaustion (no new nogood at the levelled-off level between two
    // consecutive extraction attempts — Blum & Furst's termination condition).
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

        // Snapshot the nogood count at `level` — the level the graph levelled off at. A total
        // count across all levels is useless here: every failed extraction records the goal set
        // under the brand-new top level, so the total grows on every attempt by construction.
        // The count at one fixed level is monotone and bounded, so equality between two
        // consecutive snapshots means the whole search stage added nothing.
        public void MarkExpansion(int level) {
            _sizeAtPreviousExpansion = _sizeAtLastExpansion;
            _sizeAtLastExpansion = CountAt(level);
        }

        // Stable iff the watched level's nogood set didn't grow between the two most recent
        // checkpoints, i.e. the extraction search cannot make further progress.
        public bool IsStable() {
            return _sizeAtLastExpansion >= 0
                && _sizeAtPreviousExpansion == _sizeAtLastExpansion;
        }

        private int CountAt(int level) {
            return _byLevel.TryGetValue(level, out var set) ? set.Count : 0;
        }
    }
}
