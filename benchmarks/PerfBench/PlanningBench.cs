using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace PerfBench {
    // GraphPlan scaling benchmarks for the joint multi-agent planning experiment (agents
    // encoded as distinct FOL constants, all goals solved in one GpProblem — the correctness
    // tests live in AIPlanning.Tests/MultiAgentPlanningTests). Findings:
    //
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
    //
    // Baselines measured May 2026 (Debug, net8.0): single-agent trees 1/2/5/10/20 →
    // ~15/4/26/204/2529 ms; joint 2-agent by agents 1/2/3/4 → ~15/12/27/85 ms; by trees
    // 1/2/3/5 → ~7/21/107/2419 ms (10 trees → minutes). Cost is driven by grounded Move
    // instances (subjects × locations²) and per-layer mutex work, not raw fact count.
    static class PlanningBench {
        static readonly GpActionFactory Factory = new GpActionFactory();

        public static void Run(string[] args) {
            if (Array.IndexOf(args, "stress") >= 0) {
                StressTwoAgentsTenTrees();
                return;
            }

            ScalingByAgentCount();
            Console.WriteLine();
            ScalingByTreeCount();
            Console.WriteLine();
            ChopThenWorkScalingByTreeCount();
            Console.WriteLine();
            ProfileGroundingShare();
        }

        static GpAction MakeMove() => Factory.Create("Move",
            new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
            new() { "At(z, x)", "-At(z, y)" });

        static GpAction MakeChop() => Factory.Create("Chop",
            new() { "At(z, x)", "Subject(z)", "Tree(x)", "-HasItem(z, Wood)", "HasItem(z, Axe)" },
            new() { "HasItem(z, Wood)" });

        static GpAction MakeWork() => Factory.Create("Work",
            new() { "At(z, y)", "Subject(z)", "Workplace(y)", "HasItem(z, Wood)" },
            new() { "Have(z, Wage)", "-HasItem(z, Wood)" });

        static IEnumerable<string> AgentFacts(
            string name, IEnumerable<string> trees, IEnumerable<string> workplaces) {
            yield return $"At({name}, {name}Loc)";
            yield return $"Subject({name})";
            yield return $"-HasItem({name}, Wood)";
            yield return $"HasItem({name}, Axe)";

            foreach (var t in trees)      yield return $"-At({name}, {t})";
            foreach (var w in workplaces) yield return $"-At({name}, {w})";
        }

        static GpProblem BuildJointWorkProblem(
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

        static GpSolution SolveOrThrow(GpProblem problem, string label) {
            var solution = problem.Solve();
            if (solution.IsEmpty) {
                throw new InvalidOperationException($"expected a plan for: {label}");
            }
            return solution;
        }

        static void ScalingByAgentCount() {
            Console.WriteLine("Multi-agent scaling by agent count (1 tree, 1 workplace)");
            Console.WriteLine($"{"agents",8}  {"state facts",12}  {"ms",10}  {"plan steps",12}");
            Console.WriteLine(new string('-', 50));

            var trees      = new[] { "TreeA" };
            var workplaces = new[] { "YardA" };

            foreach (var n in new[] { 1, 2, 3, 4 }) {
                var agents = Enumerable.Range(0, n)
                    .Select(i => new[] { "Alice", "Bob", "Carol", "David" }[i])
                    .ToArray();

                var problem   = BuildJointWorkProblem(agents, trees, workplaces);
                var factCount = problem.InitialState.Count;

                var sw = Stopwatch.StartNew();
                var solution = SolveOrThrow(problem, $"{n} agents");
                sw.Stop();

                var steps = solution.GetSolution(0).Count;
                Console.WriteLine($"{n,8}  {factCount,12}  {sw.ElapsedMilliseconds,10}  {steps,12}");
            }

            Console.WriteLine(
                "\nNote: each additional agent multiplies grounded-action count by ~(1 + entities/agent),\n" +
                "and mutex checking is O(groundings²) per layer — hence super-linear growth.");
        }

        static void ScalingByTreeCount() {
            Console.WriteLine("2-agent scaling by tree count");
            Console.WriteLine($"{"trees",8}  {"state facts",12}  {"ms",10}");
            Console.WriteLine(new string('-', 35));

            // 10 trees × 2 agents often takes tens of seconds; cap the sweep at 8.
            foreach (var treeCount in new[] { 1, 2, 3, 5, 8 }) {
                var trees      = Enumerable.Range(0, treeCount).Select(i => $"Tree{i}").ToArray();
                var workplaces = new[] { "YardA" };
                var agents     = new[] { "Alice", "Bob" };

                var problem   = BuildJointWorkProblem(agents, trees, workplaces);
                var factCount = problem.InitialState.Count;

                var sw = Stopwatch.StartNew();
                SolveOrThrow(problem, $"2 agents, {treeCount} trees");
                sw.Stop();

                Console.WriteLine($"{treeCount,8}  {factCount,12}  {sw.ElapsedMilliseconds,10}");
            }
        }

        static void StressTwoAgentsTenTrees() {
            Console.WriteLine("Stress — 2 agents, 10 trees; can take minutes");

            var problem = BuildJointWorkProblem(
                agents: new[] { "Alice", "Bob" },
                trees: Enumerable.Range(0, 10).Select(i => $"Tree{i}").ToArray(),
                workplaces: new[] { "YardA" });

            var sw = Stopwatch.StartNew();
            SolveOrThrow(problem, "2 agents, 10 trees");
            sw.Stop();

            Console.WriteLine($"2 agents, 10 trees: {sw.ElapsedMilliseconds} ms, facts={problem.InitialState.Count}");
        }

        static void ProfileGroundingShare() {
            Console.WriteLine("Profile — split solve time into grounding (OperatorGraph) vs the rest");

            var workplaces = new[] { "YardA" };
            var agents     = new[] { "Alice", "Bob" };

            // JIT warm-up so the first measured row is not penalised.
            {
                var warm = BuildJointWorkProblem(agents, new[] { "T0" }, workplaces);
                _ = new OperatorGraph(warm);
                _ = warm.Solve();
            }

            Console.WriteLine($"{"trees",6}  {"facts",6}  {"ground ms",10}  {"solve ms",10}  {"ground %",9}");
            Console.WriteLine(new string('-', 52));

            foreach (var treeCount in new[] { 3, 5, 8, 10 }) {
                var trees = Enumerable.Range(0, treeCount).Select(i => $"Tree{i}").ToArray();
                var facts = BuildJointWorkProblem(agents, trees, workplaces).InitialState.Count;

                var groundMs = double.MaxValue;
                for (var r = 0; r < 3; r++) {
                    var p = BuildJointWorkProblem(agents, trees, workplaces);
                    var sw = Stopwatch.StartNew();
                    _ = new OperatorGraph(p);
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds < groundMs) {
                        groundMs = sw.Elapsed.TotalMilliseconds;
                    }
                }

                var solveMs = double.MaxValue;
                for (var r = 0; r < 3; r++) {
                    var p = BuildJointWorkProblem(agents, trees, workplaces);
                    var sw = Stopwatch.StartNew();
                    _ = p.Solve();
                    sw.Stop();
                    if (sw.Elapsed.TotalMilliseconds < solveMs) {
                        solveMs = sw.Elapsed.TotalMilliseconds;
                    }
                }

                var pct = solveMs > 0 ? 100.0 * groundMs / solveMs : 0;
                Console.WriteLine($"{treeCount,6}  {facts,6}  {groundMs,10:F2}  {solveMs,10:F2}  {pct,8:F1}%");
            }
        }

        // Game-side, tree count maps to how many trees SubjectsController.BuildIsStateStrings
        // emits per replan; used to pick the cap (10 → frame freeze in Unity).
        static void ChopThenWorkScalingByTreeCount() {
            Console.WriteLine("Single-agent chop+work scaling by tree count");

            foreach (var treeCount in new[] { 1, 2, 5, 10, 20 }) {
                var initial = new List<string> {
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
                var move = MakeMove();
                var chop = Factory.Create("Chop",
                    new() { "At(z, x)", "Subject(z)", "Tree(x)", "-Carries(z, Wood)" },
                    new() { "Carries(z, Wood)" });
                var work = Factory.Create("Work",
                    new() { "At(z, y)", "Subject(z)", "Workplace(y)", "Carries(z, Wood)" },
                    new() { "Have(z, Wage)", "-Carries(z, Wood)" });

                var sw = Stopwatch.StartNew();
                var problem = new GpProblem(initialState, goals, new() { move, chop, work });
                SolveOrThrow(problem, $"chop+work, {treeCount} trees");
                sw.Stop();

                Console.WriteLine($"trees={treeCount,3}  solve={sw.ElapsedMilliseconds,6} ms");
            }
        }
    }
}
