using System;
using System.Collections.Generic;
using System.Diagnostics;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // End-to-end tests on GraphPlanAlgo.Run via GpProblem.Solve.
    // These guard against algorithmic regressions in the planner.
    [TestFixture]
    public class GraphPlanAlgorithmTests : PlanningTestBase {
        [Test]
        public void CakeProblem_FindsFiveStepPlan() {
            var problem = BuildCakeProblem();

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False, "expected a plan for the cake problem");
            Assert.That(solution.Count, Is.GreaterThanOrEqualTo(1));

            var actions = solution.GetSolution(0);
            Assert.That(actions.Keys, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }),
                "cake plan should have exactly 5 action layers (steps 0..4)");

            var expectedNames = new[] { "Move", "Work", "Move", "BuyFood", "Move" };
            for (var step = 0; step < expectedNames.Length; step++) {
                var stepActions = actions[step].GetActions(ignorePersistence: true);
                Assert.That(stepActions, Has.Count.EqualTo(1),
                    $"step {step} must contain exactly one non-persistence action");
                Assert.That(stepActions[0].Signifier, Is.EqualTo(expectedNames[step]),
                    $"step {step} should be a {expectedNames[step]} action");
            }
        }

        [Test]
        public void EmptyGoals_ReturnsZeroStepPlan() {
            var initialState = Factory.StringToSentence(new() { "Subject(Subject1)" });
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
            // Goal asks for Have(Diamond) but no action can ever produce it.
            var initialState = Factory.StringToSentence(new() {
                "Have(Apple)",
                "Subject(Subject1)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Diamond)" });
            var noOp = Factory.Create("NoOp", new() { "Subject(z)" }, new() { "Subject(z)" });

            var problem = new GpProblem(initialState, goals, new() { noOp });

            // Regression guard against the prior infinite loop.
            var solution = SolveWithGuard(problem, "unsolvable problem");
            Assert.That(solution.IsEmpty, Is.True,
                "unsolvable problem must yield an empty solution, not a fake plan");
        }

        [Test]
        public void GoalsAlreadySatisfied_FindsPlanViaPersistence() {
            // Initial state already contains the goal literal — a (possibly zero-step)
            // plan must come back, not an endless loop.
            var initialState = Factory.StringToSentence(new() {
                "Have(Apple)",
                "Subject(Subject1)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Apple)" });
            var noOp = Factory.Create("NoOp", new() { "Subject(z)" }, new() { "Subject(z)" });

            var problem = new GpProblem(initialState, goals, new() { noOp });
            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False);
        }

        // Reproducer for the Totalitaet "Chop never pursued" bug. Mirrors the smallest
        // shape that should chain Move → Chop → Move → Work to satisfy Have(z, Wage):
        //   - Work requires Carries(z, Wood) as input and consumes it.
        //   - Chop produces Carries(z, Wood) when subject stands at a Tree.
        //   - Move shuttles the subject between locations.
        // Live game observed steps=3 with [0] (persists only) [1] (persists only) [2] Work,
        // i.e. Work was deemed applicable WITHOUT any earlier Move/Chop. This test pins
        // down the planner's expected behaviour.
        [Test]
        public void ChopThenWork_FindsFourStepPlan() {
            var initialState = Factory.StringToSentence(new() {
                "At(Bob, MyLocation)",
                "-At(Bob, TreeA)",
                "-At(Bob, Yarda)",
                "Tree(TreeA)",
                "Workplace(Yarda)",
                "Subject(Bob)",
                "-Carries(Bob, Wood)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Bob, Wage)" });

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

            Assert.That(actions.Keys, Is.EquivalentTo(new[] { 0, 1, 2, 3 }),
                "chop+work plan should have exactly 4 action layers (steps 0..3)");

            var expectedNames = new[] { "Move", "Chop", "Move", "Work" };
            for (var step = 0; step < expectedNames.Length; step++) {
                var stepActions = actions[step].GetActions(ignorePersistence: true);
                Assert.That(stepActions, Has.Count.EqualTo(1),
                    $"step {step} must contain exactly one non-persistence action");
                Assert.That(stepActions[0].Signifier, Is.EqualTo(expectedNames[step]),
                    $"step {step} should be {expectedNames[step]}");
            }
        }

        // Scaling micro-benchmark for the chop+work shape: how does Solve() time grow
        // with the number of trees in the IsState? Game-side this maps to "how many
        // trees does SubjectsController.BuildIsStateStrings emit per replan?". Used
        // to pick the right cap (current game emits 10 → frame freeze in Unity).
        [Test, Explicit("Benchmark — run manually")]
        public void ChopThenWork_ScalingByTreeCount() {
            foreach (var treeCount in new[] { 1, 2, 5, 10, 20 }) {
                var initial = new System.Collections.Generic.List<string> {
                    "At(Bob, MyLocation)",
                    "-At(Bob, Yarda)",
                    "Workplace(Yarda)",
                    "Subject(Bob)",
                    "-Carries(Bob, Wood)"
                };
                for (var i = 0; i < treeCount; i++) {
                    initial.Add($"Tree(Tree{i})");
                    initial.Add($"-At(Bob, Tree{i})");
                }
                var initialState = Factory.StringToSentence(initial);
                var goals = Factory.StringToSentence(new() { "Have(Bob, Wage)" });
                var move = Factory.Create("Move",
                    new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
                    new() { "At(z, x)", "-At(z, y)" });
                var chop = Factory.Create("Chop",
                    new() { "At(z, x)", "Subject(z)", "Tree(x)", "-Carries(z, Wood)" },
                    new() { "Carries(z, Wood)" });
                var work = Factory.Create("Work",
                    new() { "At(z, y)", "Subject(z)", "Workplace(y)", "Carries(z, Wood)" },
                    new() { "Have(z, Wage)", "-Carries(z, Wood)" });

                var sw = Stopwatch.StartNew();
                var problem = new GpProblem(initialState, goals, new() { move, chop, work });
                var solution = problem.Solve();
                sw.Stop();

                Assert.That(solution.IsEmpty, Is.False, $"expected plan for {treeCount} trees");
                TestContext.Progress.WriteLine($"trees={treeCount,3}  solve={sw.ElapsedMilliseconds,6} ms");
            }
        }

        // Re-running a solver instance must not be poisoned by leftover state from a
        // previous run (regression guard for the static-cache problem in GpLayer).
        [Test]
        public void MultipleSolveCalls_AreIndependent() {
            var problem1 = BuildCakeProblem();
            var problem2 = BuildCakeProblem();

            var solution1 = problem1.Solve();
            var solution2 = problem2.Solve();

            Assert.That(solution1.IsEmpty, Is.False);
            Assert.That(solution2.IsEmpty, Is.False);
            Assert.That(solution1.GetSolution(0).Keys, Is.EquivalentTo(solution2.GetSolution(0).Keys));
        }

        private static GpProblem BuildCakeProblem() {
            var initialState = Factory.StringToSentence(new() {
                "At(Subject1,mylocation)",
                "-At(Subject1,Supermarket)",
                "-At(Subject1,Work)",
                "-At(Subject1,Home)",
                "-Have(Cake)",
                "Food(Cake)",
                "-Drink(Cake)",
                "Subject(Subject1)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Cake)", "At(Subject1,Home)" });

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
