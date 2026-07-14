using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Shared fixture: a fresh parser per test plus small parsing helpers.
    public abstract class TestBase {
        protected FirstOrderLogic.FirstOrderLogic Logic = null!;

        [SetUp]
        public void BaseSetup() => Logic = new FirstOrderLogic.FirstOrderLogic();

        protected ISentence S(string s) => (ISentence)Logic.TryParse(s);
        protected List<ISentence> Set(params string[] sentences) => sentences.Select(S).ToList();

        // NUnit's [Timeout] cannot abort a hung test on .NET Core (no Thread.Abort), so
        // runaway-suspect calls run in a Task and must finish within a hard wait bound.
        protected static T RunWithin<T>(TimeSpan bound, string label, Func<T> call) {
            var task = Task.Run(call);
            Assert.That(task.Wait(bound), Is.True,
                $"{label} did not finish within {bound.TotalSeconds:F0} s");
            return task.Result;
        }

        protected static void RunWithin(TimeSpan bound, string label, Action call) =>
            RunWithin(bound, label, () => { call(); return true; });
    }
}
