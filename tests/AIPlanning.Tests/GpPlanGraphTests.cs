using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;

namespace AIPlanningTests {
    // Direct tests on GpPlanGraph.ExtractSolution: honoring the goal-reachability result
    // and the first-solution (stopAtFirst) extraction mode.
    [TestFixture]
    public class GpPlanGraphTests : PlanningTestBase {
        [Test]
        public void ExtractSolution_ReturnsEmpty_WhenGoalsAreMutexAtTheLevel() {
            // A(K) and B(K) get inconsistent support at level 1 (their only supporters X and Y
            // have inconsistent effects), while C(K) stays freely achievable. The conflict-free
            // SUBSET of the goals is {C(K)} — extraction must NOT return a partial-goal plan
            // built from that subset.
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "A(K)", "B(K)", "C(K)" });
            var x = Factory.Create("X", new() { "P(K)" }, new() { "A(K)", "-B(K)" });
            var y = Factory.Create("Y", new() { "P(K)" }, new() { "B(K)" });
            var z = Factory.Create("Z", new() { "P(K)" }, new() { "C(K)" });
            var problem = new GpProblem(initialState, goals, new() { x, y, z });

            var graph = new GpPlanGraph(problem);
            graph.ExpandGraph();

            Assert.That(graph.StateNotMutex(1, problem.Goals), Is.False,
                "sanity: the goals must be present but not jointly conflict-free at level 1");

            var solution = graph.ExtractSolution(1, new NoGoods());
            Assert.That(solution.IsEmpty, Is.True,
                "goals that are not jointly reachable must yield NO solution, not a plan " +
                "that only achieves the mutex-stripped subset {C(K)}");
        }

        [Test]
        public void ExtractSolution_StopAtFirst_ReturnsExactlyOneValidPlan() {
            // Two interchangeable producers of the goal: exhaustive extraction enumerates both
            // supporter choices, first-solution mode stops after one.
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });
            var m1 = Factory.Create("M1", new() { "P(K)" }, new() { "G(K)" });
            var m2 = Factory.Create("M2", new() { "P(K)" }, new() { "G(K)" });
            var problem = new GpProblem(initialState, goals, new() { m1, m2 });

            var graph = new GpPlanGraph(problem);
            graph.ExpandGraph();
            Assert.That(graph.StateNotMutex(1, problem.Goals), Is.True, "sanity: goals reachable");

            var exhaustive = graph.ExtractSolution(1, new NoGoods());
            Assert.That(exhaustive.Count, Is.EqualTo(2),
                "exhaustive mode must enumerate both interchangeable supporters");

            var firstOnly = graph.ExtractSolution(1, new NoGoods(), stopAtFirst: true);
            Assert.That(firstOnly.Count, Is.EqualTo(1), "first-solution mode stops after one plan");

            var plan = firstOnly.GetSolution(0);
            Assert.That(plan.Keys, Is.EqualTo(new[] { 0 }), "one-step plan at step 0");
            var stepActions = plan[0].GetActions(ignorePersistence: true);
            Assert.That(stepActions, Has.Count.EqualTo(1));
            Assert.That(stepActions[0].Signifier, Is.AnyOf("M1", "M2"),
                "the plan must achieve the goal via one of the two producers");
        }

        [Test]
        public void Solve_SolvableProblem_FirstSolutionModeYieldsValidPlan() {
            // End-to-end: GraphPlanAlgo.Run uses first-solution extraction; the returned plan
            // must still be complete and valid.
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "R(K)" });
            var a1 = Factory.Create("A1", new() { "P(K)" }, new() { "Q(K)" });
            var a2 = Factory.Create("A2", new() { "Q(K)" }, new() { "R(K)" });
            var problem = new GpProblem(initialState, goals, new() { a1, a2 });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False);
            var plan = solution.GetSolution(0);
            var names = plan.OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.GetActions(ignorePersistence: true).Single().Signifier)
                .ToList();
            Assert.That(names, Is.EqualTo(new[] { "A1", "A2" }),
                "the two-step chain A1 → A2 is the only way to reach R(K)");
        }
    }
}
