using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // A GpProblem must be reusable: solving it twice has to give the same plan, and solving must
    // not mutate the problem's action list (the planner used to inject Start/Finish into it and
    // accumulate grounding state on the shared GpAction instances, crashing the second solve).
    [TestFixture]
    public class GpProblemReuseTests {
        private static readonly GpActionFactory Factory = new();

        private static List<GpAction> BuildCakeActions() {
            var move = Factory.Create("Move",
                new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
                new() { "At(z, x)", "-At(z, y)" });
            var work = Factory.Create("Work",
                new() { "At(z, Work)", "Subject(z)" },
                new() { "Have(Money)" });
            var buyFood = Factory.Create("BuyFood",
                new() { "At(z, Supermarket)", "Have(Money)", "Food(x)", "Subject(z)" },
                new() { "Have(x)", "-Have(Money)" });
            return new List<GpAction> { move, work, buyFood };
        }

        private static GpProblem BuildCakeProblem(List<GpAction> actions) {
            var initialState = Factory.StringToSentence(new() {
                "At(Subject1,mylocation)",
                "-At(Subject1,Supermarket)",
                "-At(Subject1,Work)",
                "-At(Subject1,Home)",
                "-Have(Cake)",
                "Food(Cake)",
                "Subject(Subject1)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Cake)", "At(Subject1,Home)" });
            return new GpProblem(initialState, goals, actions);
        }

        // The plan as one comparable shape: per step, the sorted non-persistence action names.
        private static List<string> PlanSignature(GpSolution solution) {
            var steps = solution.GetSolution(0);
            return steps.OrderBy(pair => pair.Key)
                .Select(pair => string.Join("+", pair.Value.GetActions(ignorePersistence: true)
                    .Select(action => action.Signifier).OrderBy(name => name)))
                .ToList();
        }

        [Test]
        public void SolveTwice_SameProblem_SamePlanAndProblemUntouched() {
            var actions = BuildCakeActions();
            var problem = BuildCakeProblem(actions);

            var first = problem.Solve();
            var second = problem.Solve();

            Assert.That(first.IsEmpty, Is.False, "first solve should find a plan");
            Assert.That(second.IsEmpty, Is.False, "second solve of the same problem should find a plan");
            Assert.That(PlanSignature(second), Is.EqualTo(PlanSignature(first)),
                "both solves must produce the same plan");

            Assert.That(problem.Actions, Has.Count.EqualTo(3),
                "Solve() must not add actions to the problem");
            Assert.That(problem.Actions.Select(a => a.Signifier),
                Is.EquivalentTo(new[] { "Move", "Work", "BuyFood" }),
                "the problem's action list must keep exactly the caller's actions");
        }

        [Test]
        public void SharedActionInstances_AcrossTwoProblems_BothSolve() {
            var actions = BuildCakeActions();
            var problem1 = BuildCakeProblem(actions);
            var problem2 = BuildCakeProblem(actions);

            var solution1 = problem1.Solve();
            var solution2 = problem2.Solve();

            Assert.That(solution1.IsEmpty, Is.False);
            Assert.That(solution2.IsEmpty, Is.False);
            Assert.That(PlanSignature(solution2), Is.EqualTo(PlanSignature(solution1)),
                "identical problems sharing action instances must yield identical plans");
            Assert.That(actions, Has.Count.EqualTo(3), "the shared action list must stay untouched");
        }
    }
}
