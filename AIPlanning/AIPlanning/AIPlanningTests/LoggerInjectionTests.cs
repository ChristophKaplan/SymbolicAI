using System.Collections.Generic;
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

            // After ResetSink the captured list must not grow when we log again.
            Logger.Log("not captured");
            Assert.That(captured, Is.Empty);
        }

        [Test]
        public void SetSink_WithNull_FallsBackToDefault() {
            var captured = new List<string>();
            Logger.SetSink(captured.Add);
            Logger.SetSink(null!);

            // Since a null sink restores the default (Console.WriteLine in non-Unity
            // environments), the previously-captured list must remain untouched.
            Logger.Log("ignored by captured");
            Assert.That(captured, Is.Empty);
        }
    }
}
