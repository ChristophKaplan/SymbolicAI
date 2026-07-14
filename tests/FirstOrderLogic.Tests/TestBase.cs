using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public abstract class TestBase {
        protected FirstOrderLogic.FirstOrderLogic Logic = null!;

        [SetUp]
        public void BaseSetup() => Logic = new FirstOrderLogic.FirstOrderLogic();

        protected ISentence S(string s) => (ISentence)Logic.Parse(s);
        protected List<ISentence> Set(params string[] sentences) => sentences.Select(S).ToList();

        // Assert the same boolean in both modes: subsumption must never change the result, only speed.
        protected void AssertResolves(string kb, string goal, bool expected) {
            Assert.That(Resolution.Resolve(S(kb), S(goal), useSubsumption: false),
                Is.EqualTo(expected), $"[no subsumption] {kb}  =>  {goal}");
            Assert.That(Resolution.Resolve(S(kb), S(goal), useSubsumption: true),
                Is.EqualTo(expected), $"[subsumption]    {kb}  =>  {goal}");
        }

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
