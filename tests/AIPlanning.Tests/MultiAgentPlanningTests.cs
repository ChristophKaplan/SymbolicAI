using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    //   - Solve time grows roughly polynomially with agent/entity count. An earlier
    //     exponential blowup was NOT the O(groundings²) mutex check but the eager
    //     cartesian-product enumeration during extraction, since replaced by Blum & Furst
    //     §3.2 incremental supporter selection (GpBeliefState.SelectSupporters): ~64 s →
    //     ~0.5 s for the 8-tree case. Residual cost is exhaustive grounding plus the
    //     O(groundings²) mutex check per layer.
    //   - A lifted planner is NOT worth building. Instrumenting CheckMutexRelations showed
    //     lifting could only remove pair-checks between independent agents (hit rate falls
    //     17.75% → 7.62% as agents grow), while under contention the hit rate stays ~30% —
    //     those conflicts must be grounded out anyway. Per-agent decomposition removes the
    //     same waste for almost no effort.
    //   - Conclusion: per-agent planning plus a coordination layer above the planner is the
    //     right design; joint planning is not production-viable for N > 2.
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

            foreach (var t in trees)     yield return $"-At({name}, {t})";
            foreach (var w in workplaces) yield return $"-At({name}, {w})";
            foreach (var h in houses ?? Enumerable.Empty<string>())
                yield return $"-At({name}, {h})";
        }

        private static GpProblem BuildJointWorkProblem(
            IReadOnlyList<string> agents,
            IReadOnlyList<string> trees,
            IReadOnlyList<string> workplaces) {

            var state = new HashSet<string>();

            foreach (var t in trees)      state.Add($"Tree({t})");
            foreach (var w in workplaces) state.Add($"Workplace({w})");

            foreach (var a in agents)
                foreach (var f in AgentFacts(a, trees, workplaces))
                    state.Add(f);

            var initialState = Factory.StringToSentence(state.ToList());
            var goals        = agents.Select(a => $"Have({a}, Wage)").ToList();
            var goalSentences = Factory.StringToSentence(goals);

            return new GpProblem(initialState, goalSentences,
                new() { MakeMove(), MakeChop(), MakeWork() });
        }

        [Test]
        public void TwoAgents_SameGoal_JointPlanFound() {
            var problem  = BuildJointWorkProblem(
                agents:     new[] { "Alice", "Bob" },
                trees:      new[] { "TreeA" },
                workplaces: new[] { "YardA" });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False,
                "joint planner must find a plan for two agents sharing the same goal type");

            var plan = solution.GetSolution(0);
            Assert.That(plan, Is.Not.Empty, "plan must have at least one action layer");
            Assert.That(plan.Keys.Count, Is.GreaterThanOrEqualTo(4),
                "chop+work for any agent needs at least 4 steps; two agents need at least the same");

            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void TwoAgents_ActionsAttributableBySubjectPrecondition() {
            var problem  = BuildJointWorkProblem(
                agents:     new[] { "Alice", "Bob" },
                trees:      new[] { "TreeA" },
                workplaces: new[] { "YardA" });

            var solution = problem.Solve();
            Assert.That(solution.IsEmpty, Is.False);

            var plan = solution.GetSolution(0);
            var allNodes = plan.Values
                .SelectMany(actionSet => actionSet.GetActionNodes)
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

        // Baselines measured May 2026 (Debug, net8.0): single-agent trees 1/2/5/10/20 →
        // ~15/4/26/204/2529 ms; joint 2-agent by agents 1/2/3/4 → ~15/12/27/85 ms; by trees
        // 1/2/3/5 → ~7/21/107/2419 ms (10 trees → minutes). Cost is driven by grounded Move
        // instances (subjects × locations²) and per-layer mutex work, not raw fact count.
        // Re-baseline with the [Explicit] benchmarks after planner changes.

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

        [Test, Explicit("Benchmark — run manually to measure multi-agent scaling")]
        public void MultiAgent_ScalingByAgentCount() {
            var trees      = new[] { "TreeA" };
            var workplaces = new[] { "YardA" };

            TestContext.Progress.WriteLine(
                $"{"agents",8}  {"state facts",12}  {"ms",10}  {"plan steps",12}");
            TestContext.Progress.WriteLine(new string('-', 50));

            foreach (var n in new[] { 1, 2, 3, 4 }) {
                var agents = Enumerable.Range(0, n)
                    .Select(i => new[] { "Alice", "Bob", "Carol", "David" }[i])
                    .ToArray();

                var problem  = BuildJointWorkProblem(agents, trees, workplaces);
                var factCount = problem.InitialState.Count;

                var sw = Stopwatch.StartNew();
                var solution = problem.Solve();
                sw.Stop();

                var steps = solution.IsEmpty ? -1 : solution.GetSolution(0).Count;
                TestContext.Progress.WriteLine(
                    $"{n,8}  {factCount,12}  {sw.ElapsedMilliseconds,10}  {steps,12}");

                Assert.That(solution.IsEmpty, Is.False, $"expected a plan for {n} agents");
            }

            TestContext.Progress.WriteLine(
                "\nNote: each additional agent multiplies grounded-action count by ~(1 + entities/agent),\n" +
                "and mutex checking is O(groundings²) per layer — hence super-linear growth.");
        }

        [Test, Explicit("Benchmark — run manually to measure entity-count impact for 2 agents")]
        public void MultiAgent_ScalingByTreeCount() {
            TestContext.Progress.WriteLine(
                $"{"trees",8}  {"state facts",12}  {"ms",10}");
            TestContext.Progress.WriteLine(new string('-', 35));

            // 10 trees × 2 agents often takes tens of seconds; cap the sweep at 8.
            foreach (var treeCount in new[] { 1, 2, 3, 5, 8 }) {
                var trees      = Enumerable.Range(0, treeCount).Select(i => $"Tree{i}").ToArray();
                var workplaces = new[] { "YardA" };
                var agents     = new[] { "Alice", "Bob" };

                var problem   = BuildJointWorkProblem(agents, trees, workplaces);
                var factCount = problem.InitialState.Count;

                var sw = Stopwatch.StartNew();
                var solution = problem.Solve();
                sw.Stop();

                TestContext.Progress.WriteLine(
                    $"{treeCount,8}  {factCount,12}  {sw.ElapsedMilliseconds,10}");

                Assert.That(solution.IsEmpty, Is.False,
                    $"expected a plan for 2 agents with {treeCount} trees");

                if (treeCount >= 5)
                    Assert.That(sw.ElapsedMilliseconds, Is.LessThan(120_000),
                        $"2-agent solve with {treeCount} trees exceeded 120 s — see optimization notes in test header");
            }
        }

        [Test, Explicit("Stress — 2 agents, 10 trees; can take minutes")]
        public void MultiAgent_Stress_TwoAgents_TenTrees() {
            var problem = BuildJointWorkProblem(
                agents: new[] { "Alice", "Bob" },
                trees: Enumerable.Range(0, 10).Select(i => $"Tree{i}").ToArray(),
                workplaces: new[] { "YardA" });

            var sw = Stopwatch.StartNew();
            var solution = problem.Solve();
            sw.Stop();

            TestContext.Progress.WriteLine($"2 agents, 10 trees: {sw.ElapsedMilliseconds} ms, facts={problem.InitialState.Count}");
            Assert.That(solution.IsEmpty, Is.False);
        }

        [Test, Explicit("Profile — split solve time into grounding (OperatorGraph) vs the rest")]
        public void Profile_GroundingShare() {
            var workplaces = new[] { "YardA" };
            var agents     = new[] { "Alice", "Bob" };

            // JIT warm-up so the first measured row is not penalised.
            {
                var warm = BuildJointWorkProblem(agents, new[] { "T0" }, workplaces);
                _ = new OperatorGraph(warm);
                _ = warm.Solve();
            }

            TestContext.Progress.WriteLine(
                $"{"trees",6}  {"facts",6}  {"ground ms",10}  {"solve ms",10}  {"ground %",9}");
            TestContext.Progress.WriteLine(new string('-', 52));

            foreach (var treeCount in new[] { 3, 5, 8, 10 }) {
                var trees = Enumerable.Range(0, treeCount).Select(i => $"Tree{i}").ToArray();
                var facts = BuildJointWorkProblem(agents, trees, workplaces).InitialState.Count;

                var groundMs = double.MaxValue;
                for (var r = 0; r < 3; r++) {
                    var p = BuildJointWorkProblem(agents, trees, workplaces);
                    var sw = Stopwatch.StartNew();
                    _ = new OperatorGraph(p);
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds < groundMs) groundMs = sw.Elapsed.TotalMilliseconds;
                }

                var solveMs = double.MaxValue;
                for (var r = 0; r < 3; r++) {
                    var p = BuildJointWorkProblem(agents, trees, workplaces);
                    var sw = Stopwatch.StartNew();
                    _ = p.Solve();
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds < solveMs) solveMs = sw.Elapsed.TotalMilliseconds;
                }

                var pct = solveMs > 0 ? 100.0 * groundMs / solveMs : 0;
                TestContext.Progress.WriteLine(
                    $"{treeCount,6}  {facts,6}  {groundMs,10:F2}  {solveMs,10:F2}  {pct,8:F1}%");
            }
        }
    }
}
