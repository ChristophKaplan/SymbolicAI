namespace AIPlanning.Planning.GraphPlan {
    public class GraphPlanAlgo {
        // Returns an empty GpSolution (IsEmpty == true) when the problem has no plan.
        public GpSolution Run(GpProblem problem) {
            // Trivial case: no goals to achieve.
            if (problem.Goals.Count == 0) {
                return GpSolution.EmptyPlan();
            }

            var graph = new GpPlanGraph(problem);
            var noGoods = new NoGoods();
            var levelIndex = 0;
            var extractionAttemptedSinceStable = false;

            while (true) {
                var goalsReachable = graph.StateNotMutex(levelIndex, problem.Goals);
                var graphStable = graph.Stable(levelIndex);

                if (goalsReachable) {
                    var solution = graph.ExtractSolution(levelIndex, noGoods);
                    if (!solution.IsEmpty) {
                        return solution;
                    }

                    // Termination per Blum/Furst: when the graph has levelled off and a
                    // fresh extraction attempt produced no new nogoods, no plan exists.
                    if (graphStable && extractionAttemptedSinceStable && noGoods.IsStable()) {
                        return new GpSolution();
                    }

                    noGoods.MarkExpansion();
                    if (graphStable) {
                        extractionAttemptedSinceStable = true;
                    }
                }
                else if (graphStable) {
                    // Graph cannot produce new literals AND goals are unreachable here;
                    // they will never become reachable. No plan exists.
                    return new GpSolution();
                }

                graph.ExpandGraph();
                levelIndex++;
            }
        }
    }
}
