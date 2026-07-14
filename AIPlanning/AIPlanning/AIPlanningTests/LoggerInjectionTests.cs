using System;
using System.Collections.Generic;
using System.IO;
using LogHelper;

namespace AIPlanningTests {
    // The Logger is part of the Unity-compatibility surface (skill A): consumers must
    // be able to redirect log output via SetSink, and ResetSink must restore the default.
    [TestFixture]
    public class LoggerInjectionTests {
        [TearDown]
        public void TearDown() {
            // Make sure individual tests do not affect each other's logging state.
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

            // "Restored" means the message actually reaches the default sink (Console.Out in
            // non-Unity environments) again — a null sink dropping all output must fail here.
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
