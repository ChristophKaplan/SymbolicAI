using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // Multi-agent planning experiment: agents encoded as distinct FOL constants ("Alice",
    // "Bob") instead of the single "mySelf" placeholder, all goals solved jointly in one
    // GpProblem. The implementation left production code; these tests preserve the findings:
    //
    //   - Joint planning is correct: grounded actions carry Subject(<agent>), enabling
    //     per-agent plan extraction; heterogeneous goals and shared resources work.
    //   - Joint planning does not scale (see PlanningBench in benchmarks/PerfBench for the
    //     scaling sweeps, baselines, and optimization notes): per-agent planning plus a
    //     coordination layer above the planner is the right design; joint planning is not
    //     production-viable for N > 2.
    [TestFixture]
    public class MultiAgentPlanningTests : PlanningTestBase {
        private static GpAction MakeMove() => Factory.Create("Move",
            new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
            new() { "At(z, x)", "-At(z, y)" });

        private static GpAction MakeChop() => Factory.Create("Chop",
            new() { "At(z, x)", "Subject(z)", "Tree(x)", "-HasItem(z, Wood)", "HasItem(z, Axe)" },
            new() { "HasItem(z, Wood)" });

        private static GpAction MakeWork() => Factory.Create("Work",
            new() { "At(z, y)", "Subject(z)", "Workplace(y)", "HasItem(z, Wood)" },
            new() { "Have(z, Wage)", "-HasItem(z, Wood)" });

        private static GpAction MakeRest() => Factory.Create("Rest",
            new() { "At(z, y)", "Subject(z)", "House(y)" },
            new() { "Have(z, Energy)" });

        // Each agent starts at a unique "<name>Loc" constant (the game's convention) so
        // Move can resolve the starting point.
        private static IEnumerable<string> AgentFacts(
            string name, IEnumerable<string> trees, IEnumerable<string> workplaces,
            IEnumerable<string>? houses = null) {
            yield return $"At({name}, {name}Loc)";
            yield return $"Subject({name})";
            yield return $"-HasItem({name}, Wood)";
            yield return $"HasItem({name}, Axe)";

            foreach (var t in trees) {
                yield return $"-At({name}, {t})";
            }

            foreach (var w in workplaces) {
                yield return $"-At({name}, {w})";
            }

            foreach (var h in houses ?? Enumerable.Empty<string>()) {
                yield return $"-At({name}, {h})";
            }
        }

        private static GpProblem BuildJointWorkProblem(
            IReadOnlyList<string> agents,
            IReadOnlyList<string> trees,
            IReadOnlyList<string> workplaces) {

            var state = new HashSet<string>();

            foreach (var t in trees) {
                state.Add($"Tree({t})");
            }

            foreach (var w in workplaces) {
                state.Add($"Workplace({w})");
            }

            foreach (var a in agents) {
                foreach (var f in AgentFacts(a, trees, workplaces)) {
                    state.Add(f);
                }
            }

            var initialState = Factory.StringToSentence(state.ToList());
            var goals        = agents.Select(a => $"Have({a}, Wage)").ToList();
            var goalSentences = Factory.StringToSentence(goals);

            return new GpProblem(initialState, goalSentences,
                new() { MakeMove(), MakeChop(), MakeWork() });
        }

        [Test]
        public void TwoAgents_SameGoal_JointPlanFound_AndAttributableBySubject() {
            var problem  = BuildJointWorkProblem(
                agents:     new[] { "Alice", "Bob" },
                trees:      new[] { "TreeA" },
                workplaces: new[] { "YardA" });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False,
                "joint planner must find a plan for two agents sharing the same goal type");

            var plan = solution.GetSolution(0);
            Assert.That(plan, Is.Not.Empty, "plan must have at least one action layer");
            Assert.That(plan.Count, Is.GreaterThanOrEqualTo(4),
                "chop+work for any agent needs at least 4 steps; two agents need at least the same");

            AssertPlanIsValid(problem, solution);

            var allNodes = plan
                .SelectMany(actionSet => actionSet.Nodes)
                .Where(n => !n.IsPersistenceAction)
                .ToList();

            Assert.That(allNodes, Is.Not.Empty, "joint plan must contain non-persistence actions");

            foreach (var node in allNodes) {
                var hasSubjectPrecondition = node.GpAction.Preconditions
                    .Any(p => p.ToString() == "Subject(Alice)" || p.ToString() == "Subject(Bob)");

                Assert.That(hasSubjectPrecondition, Is.True,
                    $"action '{node.GpAction.Signifier}' must carry Subject(Alice) or " +
                    $"Subject(Bob) in its grounded preconditions so the plan can be split per agent. " +
                    $"Preconditions: [{string.Join(", ", node.GpAction.Preconditions)}]");
            }
        }

        [Test]
        public void TwoAgents_DifferentGoals_BothSatisfied() {
            var state = new List<string> {
                "Tree(TreeA)", "Workplace(YardA)", "House(HomeA)",
                "At(Alice, AliceLoc)", "Subject(Alice)",
                "-HasItem(Alice, Wood)", "HasItem(Alice, Axe)",
                "-At(Alice, TreeA)", "-At(Alice, YardA)", "-At(Alice, HomeA)",
                "At(Bob, BobLoc)", "Subject(Bob)",
                "-HasItem(Bob, Wood)", "HasItem(Bob, Axe)",
                "-At(Bob, TreeA)", "-At(Bob, YardA)", "-At(Bob, HomeA)",
            };

            var goals = new List<string> { "Have(Alice, Wage)", "Have(Bob, Energy)" };

            var problem = new GpProblem(
                Factory.StringToSentence(state),
                Factory.StringToSentence(goals),
                new() { MakeMove(), MakeChop(), MakeWork(), MakeRest() });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False,
                "joint planner must satisfy heterogeneous goals (Wage for Alice, Energy for Bob)");

            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void TwoAgents_SharedTree_NoMutexConflict() {
            // Chop(Alice, TreeA) and Chop(Bob, TreeA) produce independent effects and delete
            // nothing of each other's, so they are mutex-free and may share a plan layer.
            var state = new List<string> {
                "Tree(TreeA)", "Workplace(YardA)",
                "At(Alice, TreeA)", "Subject(Alice)", "-HasItem(Alice, Wood)", "HasItem(Alice, Axe)",
                "-At(Alice, YardA)",
                "At(Bob,   TreeA)", "Subject(Bob)",   "-HasItem(Bob,   Wood)", "HasItem(Bob,   Axe)",
                "-At(Bob,   YardA)",
            };
            var goals = Factory.StringToSentence(
                new() { "Have(Alice, Wage)", "Have(Bob, Wage)" });

            var problem = new GpProblem(
                Factory.StringToSentence(state),
                goals,
                new() { MakeMove(), MakeChop(), MakeWork() });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False,
                "two agents at the same tree must not cause a mutex deadlock in the planner");
            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void MultiAgent_GameLikeTwoAgents_ThreeTrees_UnderBudget() {
            var trees = new[] { "TreeA", "TreeB", "TreeC" };
            var problem = BuildJointWorkProblem(
                agents: new[] { "Alice", "Bob" },
                trees: trees,
                workplaces: new[] { "YardA" });

            var solution = SolveWithGuard(problem, "2-agent / 3-tree joint solve");

            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void FourAgents_SmallWorld_TerminatesWithinBudget() {
            var problem = BuildJointWorkProblem(
                agents:     new[] { "Alice", "Bob", "Carol", "David" },
                trees:      new[] { "TreeA" },
                workplaces: new[] { "YardA" });

            var solution = SolveWithGuard(problem, "4-agent joint solve (1 tree, 1 workplace)");

            Assert.That(solution.IsEmpty, Is.False,
                "4 agents in a small world must find a valid joint plan");
            AssertPlanIsValid(problem, solution);
        }
    }
}
