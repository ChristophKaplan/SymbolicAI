namespace AIPlanning.Planning.GraphPlan {
    public class GraphPlanAlgo {
        // Returns an empty GpSolution (IsEmpty == true) when the problem has no plan.
        public GpSolution Run(GpProblem problem) {
            // Trivial case: no goals to achieve.
            if (problem.Goals.Count == 0) {
                return GpSolution.EmptyPlan();
            }

            var graph = new GpPlanGraph(problem);

            // Goals already hold in the initial state: nothing to do.
            if (graph.StateNotMutex(0, problem.Goals)) {
                return GpSolution.EmptyPlan();
            }

            var noGoods = new NoGoods();
            var levelIndex = 0;
            var levelledOffAt = -1;

            while (true) {
                var goalsReachable = graph.StateNotMutex(levelIndex, problem.Goals);
                var graphStable = graph.Stable(levelIndex);
                if (graphStable && levelledOffAt < 0) {
                    levelledOffAt = levelIndex;
                }

                if (goalsReachable) {
                    // One plan is enough for execution — skip the exhaustive (worst-case
                    // exponential) enumeration of every interchangeable supporter combination.
                    var solution = graph.ExtractSolution(levelIndex, noGoods, stopAtFirst: true);
                    if (!solution.IsEmpty) {
                        return solution;
                    }

                    // Termination per Blum/Furst: once the graph has levelled off, snapshot the
                    // nogood count at the levelled-off level after each failed extraction. Two
                    // equal consecutive snapshots mean a whole search stage discovered nothing
                    // new — further stages explore the same space and can never succeed.
                    if (levelledOffAt >= 0) {
                        noGoods.MarkExpansion(levelledOffAt);
                        if (noGoods.IsStable()) {
                            return new GpSolution();
                        }
                    }
                }
                else if (graphStable) {
                    // Graph can produce no new literals and relax no mutexes AND the goals are
                    // not jointly reachable here; they never will be. No plan exists.
                    return new GpSolution();
                }

                graph.ExpandGraph();
                levelIndex++;
            }
        }
    }
}
