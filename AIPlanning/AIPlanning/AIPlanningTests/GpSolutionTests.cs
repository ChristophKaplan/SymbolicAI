using AIPlanning.Planning.GraphPlan;

namespace AIPlanningTests {
    [TestFixture]
    public class GpSolutionTests {
        [Test]
        public void DefaultSolution_IsEmpty() {
            var solution = new GpSolution();

            Assert.That(solution.IsEmpty, Is.True);
            Assert.That(solution.Count, Is.EqualTo(0));
        }

        [Test]
        public void EmptyPlan_IsNotEmpty_ButHasZeroSteps() {
            var solution = GpSolution.EmptyPlan();

            Assert.That(solution.IsEmpty, Is.False,
                "EmptyPlan() represents a successful 0-step plan, not the absence of a solution");
            Assert.That(solution.Count, Is.EqualTo(1));
            Assert.That(solution.GetSolution(0), Is.Empty);
        }

        [Test]
        public void EmptySolution_ToString_ContainsExplanation() {
            var solution = new GpSolution();

            Assert.That(solution.ToString(), Does.Contain("No solutions"));
        }
    }
}
