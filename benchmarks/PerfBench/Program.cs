using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using FirstOrderLogic;

namespace PerfBench {
    class Program {
        static string Clause(params string[] literals) {
            var s = literals[0];
            for (var i = 1; i < literals.Length; i++) {
                s = "(" + s + " OR " + literals[i] + ")";
            }

            return s;
        }

        static string Conjoin(List<string> clauses) {
            var s = clauses[0];
            for (var i = 1; i < clauses.Count; i++) {
                s = "(" + s + " AND " + clauses[i] + ")";
            }

            return s;
        }

        static string BuildChainKb(int chainLen, int noise, int tautologies) {
            var clauses = new List<string> { "P0" };
            for (var k = 1; k <= chainLen; k++) {
                clauses.Add(Clause("(NOT P" + (k - 1) + ")", "P" + k));
            }

            for (var i = 0; i < noise; i++) {
                clauses.Add("K" + i);
            }

            for (var i = 0; i < tautologies; i++) {
                clauses.Add(Clause("T" + i, "(NOT T" + i + ")"));
            }

            return Conjoin(clauses);
        }

        static void TimeResolve(string kbText, string goalText, int iterations,
            out double minMs, out double avgMs, out bool result) {
            var logic = new FirstOrderLogic.FirstOrderLogic();
            var kb = (ISentence)logic.Parse(kbText);
            var goal = (ISentence)logic.Parse(goalText);

            result = Resolution.Resolve(kb, goal); // warmup

            minMs = double.MaxValue;
            var totalMs = 0.0;
            for (var i = 0; i < iterations; i++) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var sw = Stopwatch.StartNew();
                result = Resolution.Resolve(kb, goal);
                sw.Stop();
                var ms = sw.Elapsed.TotalMilliseconds;
                totalMs += ms;
                if (ms < minMs) {
                    minMs = ms;
                }
            }
            avgMs = totalMs / iterations;
        }

        static void Run(string label, int chainLen, int noise, int tautologies, int iterations) {
            var kb = BuildChainKb(chainLen, noise, tautologies);
            var goal = "Pbogus"; // not in KB -> not entailed -> full saturation
            TimeResolve(kb, goal, iterations, out var min, out var avg, out var result);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-8} chain={1,3} noise={2,4} taut={3,3} -> min {4,9:F3} ms | avg {5,9:F3} ms  (result={6})",
                label, chainLen, noise, tautologies, min, avg, result));
        }

        static void RunKernels(string label, int noise, int iterations) {
            var ks = new KernelSets();
            var b = new List<ISentence> {
                S("P(a)"), S("P(x) => S(x)"),
                S("R(a)"), S("R(x) => S(x)"),
                S("T(a)"), S("T(x) => S(x)"),
            };
            for (var i = 0; i < noise; i++) {
                b.Add(S("Irr" + i + "(a)"));
            }

            var alpha = S("S(a)");

            var count = ks.FindAllKernels(b, alpha).Count; // warmup

            var min = double.MaxValue;
            for (var i = 0; i < iterations; i++) {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var sw = Stopwatch.StartNew();
                ks.FindAllKernels(b, alpha);
                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds < min) {
                    min = sw.Elapsed.TotalMilliseconds;
                }
            }
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,-8} noise={1,3} -> min {2,9:F3} ms  (kernels={3})", label, noise, min, count));
        }

        static ISentence S(string text) =>
            (ISentence)new FirstOrderLogic.FirstOrderLogic().Parse(text);

        // "dotnet run -- planning" runs the GraphPlan scaling sweeps ("planning stress" for
        // the long 10-tree case); no arguments runs the FOL benchmarks.
        static void Main(string[] args) {
            if (args.Length > 0 && args[0] == "planning") {
                PlanningBench.Run(args);
                return;
            }

            Console.WriteLine("Resolution saturation benchmark (full saturation, not entailed)");
            Console.WriteLine("===============================================================");
            Run("small",  10,  20,  0, 80);
            Run("medium", 14,  40,  0, 50);
            Run("large",  18,  60,  0, 30);
            Run("xlarge", 22,  80,  0, 20);
            Run("taut",   14,  20, 25, 20);

            Console.WriteLine();
            Console.WriteLine("KernelSets.FindAllKernels benchmark");
            Console.WriteLine("===================================");
            RunKernels("k-small",  3, 20);
            RunKernels("k-medium", 6, 10);
            RunKernels("k-large",  9, 6);
        }
    }
}
