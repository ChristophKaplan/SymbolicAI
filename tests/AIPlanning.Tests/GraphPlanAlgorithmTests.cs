using System.Collections.Generic;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    [TestFixture]
    public class GraphPlanAlgorithmTests : PlanningTestBase {
        [Test]
        public void CakeProblem_FindsFiveStepPlan() {
            var problem = BuildCakeProblem();

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False, "expected a plan for the cake problem");
            Assert.That(solution.Count, Is.GreaterThanOrEqualTo(1));

            var actions = solution.GetSolution(0);
            Assert.That(actions, Has.Count.EqualTo(5),
                "cake plan should have exactly 5 steps");

            var expectedNames = new[] { "Move", "Work", "Move", "BuyFood", "Move" };
            for (var step = 0; step < expectedNames.Length; step++) {
                var stepActions = actions[step].GetActions(ignorePersistence: true);
                Assert.That(stepActions, Has.Count.EqualTo(1),
                    $"step {step} must contain exactly one non-persistence action");
                Assert.That(stepActions[0].Name, Is.EqualTo(expectedNames[step]),
                    $"step {step} should be a {expectedNames[step]} action");
            }
        }

        [Test]
        public void EmptyGoals_ReturnsZeroStepPlan() {
            var initialState = Factory.ParseSentences(new() { "Subject(Subject1)" });
            var emptyGoals = new List<ISentence>();
            var problem = new GpProblem(initialState, emptyGoals, new List<GpAction>());

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False, "an empty goal set is trivially achieved");
            Assert.That(solution.Count, Is.EqualTo(1));
            Assert.That(solution.GetSolution(0), Is.Empty,
                "the solution for empty goals must contain zero action layers");
        }

        [Test]
        public void UnsolvableProblem_TerminatesWithEmptySolution() {
            var initialState = Factory.ParseSentences(new() {
                "Have(Apple)",
                "Subject(Subject1)"
            });
            var goals = Factory.ParseSentences(new() { "Have(Diamond)" });
            var noOp = Factory.Create("NoOp", new() { "Subject(z)" }, new() { "Subject(z)" });

            var problem = new GpProblem(initialState, goals, new() { noOp });

            var solution = SolveWithGuard(problem, "unsolvable problem");
            Assert.That(solution.IsEmpty, Is.True,
                "unsolvable problem must yield an empty solution, not a fake plan");
        }

        [Test]
        public void GoalsAlreadySatisfied_FindsPlanViaPersistence() {
            var initialState = Factory.ParseSentences(new() {
                "Have(Apple)",
                "Subject(Subject1)"
            });
            var goals = Factory.ParseSentences(new() { "Have(Apple)" });
            var noOp = Factory.Create("NoOp", new() { "Subject(z)" }, new() { "Subject(z)" });

            var problem = new GpProblem(initialState, goals, new() { noOp });
            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False);
        }

        // Reproducer for the Totalitaet "Chop never pursued" bug: the live game observed a
        // 3-step plan of persists + Work, i.e. Work deemed applicable without any earlier
        // Move/Chop ever producing Carries(z, Wood).
        [Test]
        public void ChopThenWork_FindsFourStepPlan() {
            var initialState = Factory.ParseSentences(new() {
                "At(Bob, MyLocation)",
                "-At(Bob, TreeA)",
                "-At(Bob, Yarda)",
                "Tree(TreeA)",
                "Workplace(Yarda)",
                "Subject(Bob)",
                "-Carries(Bob, Wood)"
            });
            var goals = Factory.ParseSentences(new() { "Have(Bob, Wage)" });

            var move = Factory.Create("Move",
                new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
                new() { "At(z, x)", "-At(z, y)" });
            var chop = Factory.Create("Chop",
                new() { "At(z, x)", "Subject(z)", "Tree(x)", "-Carries(z, Wood)" },
                new() { "Carries(z, Wood)" });
            var work = Factory.Create("Work",
                new() { "At(z, y)", "Subject(z)", "Workplace(y)", "Carries(z, Wood)" },
                new() { "Have(z, Wage)", "-Carries(z, Wood)" });

            var problem = new GpProblem(initialState, goals, new() { move, chop, work });
            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False, "expected a plan for chop+work");
            var actions = solution.GetSolution(0);

            Assert.That(actions, Has.Count.EqualTo(4),
                "chop+work plan should have exactly 4 steps");

            var expectedNames = new[] { "Move", "Chop", "Move", "Work" };
            for (var step = 0; step < expectedNames.Length; step++) {
                var stepActions = actions[step].GetActions(ignorePersistence: true);
                Assert.That(stepActions, Has.Count.EqualTo(1),
                    $"step {step} must contain exactly one non-persistence action");
                Assert.That(stepActions[0].Name, Is.EqualTo(expectedNames[step]),
                    $"step {step} should be {expectedNames[step]}");
            }
        }

        // Regression guard for the static-cache problem in GpLayer.
        [Test]
        public void MultipleSolveCalls_AreIndependent() {
            var problem1 = BuildCakeProblem();
            var problem2 = BuildCakeProblem();

            var solution1 = problem1.Solve();
            var solution2 = problem2.Solve();

            Assert.That(solution1.IsEmpty, Is.False);
            Assert.That(solution2.IsEmpty, Is.False);
            Assert.That(solution1.GetSolution(0).Count, Is.EqualTo(solution2.GetSolution(0).Count));
        }

        private static GpProblem BuildCakeProblem() {
            var initialState = Factory.ParseSentences(new() {
                "At(Subject1,mylocation)",
                "-At(Subject1,Supermarket)",
                "-At(Subject1,Work)",
                "-At(Subject1,Home)",
                "-Have(Cake)",
                "Food(Cake)",
                "-Drink(Cake)",
                "Subject(Subject1)"
            });
            var goals = Factory.ParseSentences(new() { "Have(Cake)", "At(Subject1,Home)" });

            var work = Factory.Create("Work",
                new() { "At(z, Work)", "Subject(z)" },
                new() { "Have(Money)" });
            var buyFood = Factory.Create("BuyFood",
                new() { "At(z, Supermarket)", "Have(Money)", "Food(x)", "Subject(z)" },
                new() { "Have(x)", "-Have(Money)" });
            var move = Factory.Create("Move",
                new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
                new() { "At(z, x)", "-At(z, y)" });

            return new GpProblem(initialState, goals, new() { move, work, buyFood });
        }
    }
}
