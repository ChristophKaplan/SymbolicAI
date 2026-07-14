using System;
using System.Collections.Generic;
using System.IO;
using LogHelper;

namespace AIPlanningTests {
    // The Logger sink is part of the Unity-compatibility surface: consumers must be able
    // to redirect log output via SetSink.
    [TestFixture]
    public class LoggerInjectionTests {
        [TearDown]
        public void TearDown() {
            Logger.ResetSink();
        }

        [Test]
        public void SetSink_RoutesLogCallsToCustomDelegate() {
            var captured = new List<string>();
            Logger.SetSink(captured.Add);

            Logger.Log("hello");
            Logger.Log("world");

            Assert.That(captured, Is.EqualTo(new[] { "hello", "world" }));
        }

        [Test]
        public void ResetSink_RestoresDefaultBehaviour() {
            var captured = new List<string>();
            Logger.SetSink(captured.Add);
            Logger.ResetSink();

            var console = CaptureConsoleOut(() => Logger.Log("back to default"));

            Assert.That(captured, Is.Empty, "the custom sink must no longer receive messages");
            Assert.That(console, Does.Contain("back to default"));
        }

        [Test]
        public void SetSink_WithNull_FallsBackToDefault() {
            var captured = new List<string>();
            Logger.SetSink(captured.Add);
            Logger.SetSink(null!);

            var console = CaptureConsoleOut(() => Logger.Log("ignored by captured"));

            Assert.That(captured, Is.Empty, "the custom sink must no longer receive messages");
            Assert.That(console, Does.Contain("ignored by captured"));
        }

        private static string CaptureConsoleOut(Action action) {
            var original = Console.Out;
            var writer = new StringWriter();
            Console.SetOut(writer);
            try {
                action();
            }
            finally {
                Console.SetOut(original);
            }

            return writer.ToString();
        }
    }
}
