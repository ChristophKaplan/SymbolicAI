using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;
using NUnit.Framework;

namespace AIPlanningTests {
    // Shared fixture plumbing: the literal parser and the solve-runaway guard. NUnit's
    // [Timeout] cannot abort a hung test on .NET Core (no Thread.Abort), so runaway-suspect
    // solves run in a Task and must finish within a hard wait bound — generous, because
    // wall-clock assertions flake on loaded CI machines.
    public abstract class PlanningTestBase {
        protected static readonly GpActionFactory Factory = new();
        protected static readonly TimeSpan SolveBound = TimeSpan.FromSeconds(30);

        protected static ISentence L(string s) =>
            Factory.StringToSentence(new List<string> { s }).Single();

        protected static GpSolution SolveWithGuard(GpProblem problem, string label = "Solve()") {
            var task = Task.Run(problem.Solve);
            Assert.That(task.Wait(SolveBound), Is.True,
                $"{label} did not terminate within {SolveBound.TotalSeconds:F0} s — runaway regression");
            return task.Result;
        }
    }
}
