using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // Multi-agent planning experiments for GraphPlan.
    //
    // Motivation: Totalitaet explored using GraphPlan as a joint planner where multiple
    // agents are encoded as distinct FOL constants ("Alice", "Bob") instead of the
    // conventional single "mySelf" placeholder.  Action templates with variable z unify
    // against every agent constant in the state, producing per-agent grounded instances.
    // The planner then solves for all agents' goals simultaneously in one GpProblem.
    //
    // These tests pin down what works, what the performance curve looks like, and why
    // the approach hits a wall — so the insight is preserved without the implementation
    // staying in production code.
    //
    // Action schema (mirrors Totalitaet):
    //   Move:  [-At(z,x), At(z,y), Subject(z)]       → [At(z,x), -At(z,y)]
    //   Chop:  [At(z,x), Subject(z), Tree(x),
    //           -HasItem(z,Wood), HasItem(z,Axe)]     → [HasItem(z,Wood)]
    //   Work:  [At(z,y), Subject(z), Workplace(y),
    //           HasItem(z,Wood)]                      → [Have(z,Wage), -HasItem(z,Wood)]
    //   Rest:  [At(z,y), Subject(z), House(y)]        → [Have(z,Energy)]
    //
    // Key findings (summarised):
    //   1. Correctness — the planner does find valid joint plans.  Grounded actions carry
    //      the concrete agent term in their Subject(?) precondition, enabling per-agent
    //      plan extraction identical to FilterPlanForSubject in the game.
    //   2. Action independence — two agents can occupy the same tree simultaneously
    //      (no mutex) because Chop produces HasItem(Alice,Wood) / HasItem(Bob,Wood)
    //      independently; neither deletes the other's fact.
    //   3. Different goals — a single joint problem satisfies heterogeneous OughtStates
    //      (Wage for one agent, Energy for another) without conflict.
    //   4. Performance — solve time grows roughly polynomially with agent/entity count.
    //      An earlier *exponential* blowup was NOT the O(groundings²) mutex check, as once
    //      assumed, but the eager cartesian-product enumeration of conflict-free action sets
    //      during extraction. That is now Blum & Furst §3.2 incremental supporter selection
    //      (GpBeliefState.SelectSupporters), which took the 8-tree case from ~64 s to ~0.5 s.
    //      Residual cost is exhaustive grounding + the O(groundings²) mutex check per layer.
    //      See the [Explicit] benchmarks for measured numbers.
    //   5. Architectural conclusion — per-agent planning + a coordination layer above
    //      the planner (task allocator assigning differentiated goals) is the right
    //      design.  Joint planning is interesting but not production-viable for N > 2.
    //   6. Would a *lifted* planner help?  Profiling put mutex pair-checking at ~54% of
    //      solve time — the only cost lifting (collapsing symmetric ground instances into
    //      one node) could attack. We measured the ceiling on a feasibility branch by
    //      instrumenting CheckMutexRelations to count pair-checks vs pair-checks that found
    //      a REAL mutex (hit rate = the share lifting CANNOT remove), across two axes:
    //
    //        SYMMETRIC (independent agents, 1 tree/1 yard)   CONTENTION (one exclusive yard)
    //        agents  pairChecks  mutexes  hitRate            agents  pairChecks  mutexes  hitRate
    //          2        3,301      586    17.75%               2         762      244    32.02%
    //          3        7,142      879    12.31%               3       1,577      476    30.18%
    //          4       12,447    1,172     9.42%               4       2,685      784    29.20%
    //          5       19,216    1,465     7.62%               5       4,086    1,168    28.59%
    //
    //      Symmetric hit rate FALLS as agents grow (17.75%→7.62%): adding independent agents
    //      makes an ever-larger share of mutex work wasted on instances that never conflict —
    //      a high lifting ceiling. Contention hit rate stays FLAT (~30%): those conflicts are
    //      real and irreducible — a lifted planner must still ground them out to detect them,
    //      so lifting can't help there. I.e. lifting only speeds up the cases that are already
    //      cheap (independent agents) and does nothing for the expensive ones (contention).
    //      Worse, where lifting would help, per-agent decomposition (finding #5) helps MORE for
    //      almost no effort: N separate solves never enumerate a single cross-agent pair, so
    //      they erase the entire wasted block without a lifted planner. VERDICT: a lifted
    //      planner (weeks of work, lifted-mutex-as-CSP correctness risk) is NOT worth building;
    //      it is dominated by decomposition on one side and powerless on the other.
    [TestFixture]
    public class MultiAgentPlanningTests : PlanningTestBase {
        // ── Action templates ──────────────────────────────────────────────────────────

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

        // ── State / problem builders ──────────────────────────────────────────────────

        /// <summary>
        /// Builds the initial-state strings for one agent in the joint encoding.
        /// Each agent gets a unique location constant "&lt;name&gt;Loc" (mirrors the game's
        /// "AliceLoc" / "BobLoc" convention) so Move.Init can resolve the starting point.
        /// </summary>
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

        /// <summary>
        /// Builds a joint GpProblem for the given agents, all pursuing Have(z, Wage).
        /// </summary>
        private static GpProblem BuildJointWorkProblem(
            IReadOnlyList<string> agents,
            IReadOnlyList<string> trees,
            IReadOnlyList<string> workplaces) {

            var state = new HashSet<string>();

            // Shared world facts
            foreach (var t in trees)      state.Add($"Tree({t})");
            foreach (var w in workplaces) state.Add($"Workplace({w})");

            // Per-agent facts
            foreach (var a in agents)
                foreach (var f in AgentFacts(a, trees, workplaces))
                    state.Add(f);

            var initialState = Factory.StringToSentence(state.ToList());
            var goals        = agents.Select(a => $"Have({a}, Wage)").ToList();
            var goalSentences = Factory.StringToSentence(goals);

            return new GpProblem(initialState, goalSentences,
                new() { MakeMove(), MakeChop(), MakeWork() });
        }

        // ── Correctness tests ─────────────────────────────────────────────────────────

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

            // Both goals must be achievable — if only one agent reaches Wage the planner
            // would have stopped early, meaning the second goal was already in the initial
            // state (it isn't) or the plan truly satisfies both.
            Assert.That(plan.Keys.Count, Is.GreaterThanOrEqualTo(4),
                "chop+work for any agent needs at least 4 steps; two agents need at least the same");
        }

        [Test]
        public void TwoAgents_ActionsAttributableBySubjectPrecondition() {
            // After grounding, every non-persistence action node carries the concrete agent
            // term in a Subject(??) precondition.  This is the mechanism FilterPlanForSubject
            // in the game uses to split a joint plan back into per-agent sub-plans.
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
            // Alice wants a Wage; Bob wants Energy (rest). A single joint problem must
            // satisfy both heterogeneous goals.
            var state = new List<string> {
                // Shared world
                "Tree(TreeA)", "Workplace(YardA)", "House(HomeA)",
                // Alice — needs Wage (Move→Chop→Move→Work)
                "At(Alice, AliceLoc)", "Subject(Alice)",
                "-HasItem(Alice, Wood)", "HasItem(Alice, Axe)",
                "-At(Alice, TreeA)", "-At(Alice, YardA)", "-At(Alice, HomeA)",
                // Bob — needs Energy (Move→Rest)
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
        }

        [Test]
        public void TwoAgents_SharedTree_NoMutexConflict() {
            // Two agents can chop the SAME tree simultaneously in the planner because:
            //   Chop(Alice, TreeA) produces HasItem(Alice, Wood)
            //   Chop(Bob,   TreeA) produces HasItem(Bob,   Wood)
            // Neither action deletes the other's effect or precondition, so they are
            // mutex-free and can appear in the same plan layer.
            // This test verifies the planner finds a valid plan (rather than getting stuck
            // thinking one agent "owns" the tree).
            var state = new List<string> {
                "Tree(TreeA)", "Workplace(YardA)",
                "At(Alice, TreeA)", "Subject(Alice)", "-HasItem(Alice, Wood)", "HasItem(Alice, Axe)",
                "-At(Alice, YardA)",
                "At(Bob,   TreeA)", "Subject(Bob)",   "-HasItem(Bob,   Wood)", "HasItem(Bob,   Axe)",
                "-At(Bob,   YardA)",
            };
            // Both already at the tree — skip Move, so plan should be Chop→Move→Work.
            var goals = Factory.StringToSentence(
                new() { "Have(Alice, Wage)", "Have(Bob, Wage)" });

            var problem = new GpProblem(
                Factory.StringToSentence(state),
                goals,
                new() { MakeMove(), MakeChop(), MakeWork() });

            var solution = problem.Solve();

            Assert.That(solution.IsEmpty, Is.False,
                "two agents at the same tree must not cause a mutex deadlock in the planner");
        }

        // ── Performance regression (CI) ───────────────────────────────────────────────
        //
        // Measured on a dev machine (Debug, net8.0, May 2026). Use [Explicit] benchmarks
        // to re-baseline after planner changes.
        //
        // Single-agent chop+work (GraphPlanAlgorithmTests.ChopThenWork_ScalingByTreeCount):
        //   trees  1/2/5/10/20  →  ~15 / 4 / 26 / 204 / 2529 ms
        //
        // Joint two-agent wage goal (minimal world: 1 workplace):
        //   agents 1/2/3/4     →  ~15 / 12 / 27 / 85 ms  (8–26 state facts)
        //   trees  1/2/3/5     →  ~7 / 21 / 107 / 2419 ms (2 agents; 10 trees → minutes)
        //
        // Takeaway: cost is driven by grounded Move instances (subjects × locations²) and
        // mutex work per plan-graph layer — not raw fact count alone.

        /// <summary>
        /// Shape similar to a pruned joint batch in-game (2 agents, few trees).
        /// Fails CI if OperatorGraph / grounding regresses badly (runaway guard only —
        /// use the [Explicit] benchmarks for actual timing numbers).
        /// </summary>
        [Test]
        public void MultiAgent_GameLikeTwoAgents_ThreeTrees_UnderBudget() {
            var trees = new[] { "TreeA", "TreeB", "TreeC" };
            var problem = BuildJointWorkProblem(
                agents: new[] { "Alice", "Bob" },
                trees: trees,
                workplaces: new[] { "YardA" });

            var solution = SolveWithGuard(problem, "2-agent / 3-tree joint solve");

            Assert.That(solution.IsEmpty, Is.False);
        }

        /// <summary>
        /// Stress: many agents in a tiny world must still terminate quickly.
        /// </summary>
        [Test]
        public void FourAgents_SmallWorld_TerminatesWithinBudget() {
            // Guard: even the pathological case (4 agents, sparse world) must terminate.
            // In the Totalitaet experiment with 10 trees / 10 houses the 4-agent solve
            // took > 30 s and was abandoned. With 1 tree / 1 workplace it should be fast
            // (~85 ms measured, see table above).
            var problem = BuildJointWorkProblem(
                agents:     new[] { "Alice", "Bob", "Carol", "David" },
                trees:      new[] { "TreeA" },
                workplaces: new[] { "YardA" });

            var solution = SolveWithGuard(problem, "4-agent joint solve (1 tree, 1 workplace)");

            Assert.That(solution.IsEmpty, Is.False,
                "4 agents in a small world must find a valid joint plan");
        }

        // ── Performance benchmarks ────────────────────────────────────────────────────
        //
        // Run manually:
        //   dotnet test --filter "FullyQualifiedName~MultiAgent_Scaling"

        [Test, Explicit("Benchmark — run manually to measure multi-agent scaling")]
        public void MultiAgent_ScalingByAgentCount() {
            // Solve time grows roughly polynomially with agent count: exhaustive grounding
            // plus the O(groundings²) mutex check per layer. (The former exponential blowup
            // came from eager action-set enumeration in extraction, now replaced with the
            // Blum & Furst §3.2 incremental supporter selection.)
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
            // With 2 agents fixed, how does adding more trees affect solve time?
            // Every additional tree T adds:
            //   Tree(T), -At(Alice, T), -At(Bob, T)  → 3 new state facts
            //   1 new Chop grounding per agent → 2n new grounded actions
            // The OperatorGraph grounds these once up front; each layer then filters them by
            // applicability (it does not re-ground per layer).
            TestContext.Progress.WriteLine(
                $"{"trees",8}  {"state facts",12}  {"ms",10}");
            TestContext.Progress.WriteLine(new string('-', 35));

            // 10 trees × 2 agents is a stress case (often tens of seconds); cap the sweep here.
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

                // Soft guard so the benchmark test itself does not hang the runner.
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

            // Warm up the JIT so the first measured row is not penalised.
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

                // Grounding only: build the OperatorGraph (best of 3 to cut noise).
                var groundMs = double.MaxValue;
                for (var r = 0; r < 3; r++) {
                    var p = BuildJointWorkProblem(agents, trees, workplaces);
                    var sw = Stopwatch.StartNew();
                    _ = new OperatorGraph(p);
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds < groundMs) groundMs = sw.Elapsed.TotalMilliseconds;
                }

                // Full solve (best of 3).
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
