namespace AIPlanning.Planning.GraphPlan {
    public static class GraphPlanAlgo {
        public static GpSolution Run(GpProblem problem) {
            if (problem.Goals.Count == 0) {
                return GpSolution.EmptyPlan();
            }

            var graph = new GpPlanGraph(problem);
            var noGoods = new NoGoods();
            var levelIndex = 0;
            var levelledOffAt = -1;

            while (true) {
                var goalsReachable = graph.StateNotMutex(levelIndex, problem.Goals);
                // Level-off is permanent, so stop re-comparing layers once it is known.
                var graphStable = levelledOffAt >= 0 || graph.Stable(levelIndex);
                if (graphStable && levelledOffAt < 0) {
                    levelledOffAt = levelIndex;
                }

                if (goalsReachable) {
                    var solution = graph.ExtractSolution(levelIndex, noGoods, stopAtFirst: true);
                    if (!solution.IsEmpty) {
                        return solution;
                    }

                    // Termination per Blum/Furst: after level-off, two equal consecutive nogood
                    // snapshots mean further search stages explore the same space and can never
                    // succeed.
                    if (levelledOffAt >= 0) {
                        noGoods.MarkExpansion(levelledOffAt);
                        if (noGoods.IsStable()) {
                            return new GpSolution();
                        }
                    }
                }
                else if (graphStable) {
                    // The graph can produce no new literals and relax no mutexes, and the goals
                    // are not jointly reachable here — they never will be.
                    return new GpSolution();
                }

                graph.ExpandGraph();
                levelIndex++;
            }
        }
    }
}
