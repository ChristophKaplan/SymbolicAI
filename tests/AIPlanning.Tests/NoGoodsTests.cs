using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    [TestFixture]
    public class NoGoodsTests : PlanningTestBase {
        [Test]
        public void Add_StoresMultipleStatesPerLevel() {
            var nogoods = new NoGoods();

            var stateA = StateOf("Have(Apple)");
            var stateB = StateOf("Have(Banana)");

            nogoods.Add(level: 1, stateA);
            nogoods.Add(level: 1, stateB);

            Assert.That(nogoods.Contains(1, stateA), Is.True);
            Assert.That(nogoods.Contains(1, stateB), Is.True,
                "regression: the previous implementation silently dropped subsequent inserts at the same level");
        }

        [Test]
        public void Contains_OnlyReturnsTrueForRegisteredLevel() {
            var nogoods = new NoGoods();
            var state = StateOf("Have(Apple)");

            nogoods.Add(level: 2, state);

            Assert.That(nogoods.Contains(1, state), Is.False);
            Assert.That(nogoods.Contains(2, state), Is.True);
            Assert.That(nogoods.Contains(3, state), Is.False);
        }

        [Test]
        public void Contains_UsesStructuralEquality() {
            var nogoods = new NoGoods();
            var stored = StateOf("Have(Apple)", "Subject(Subject1)");
            nogoods.Add(level: 1, stored);

            var probe = StateOf("Subject(Subject1)", "Have(Apple)");
            Assert.That(nogoods.Contains(1, probe), Is.True,
                "belief states with the same literals must be considered equal nogoods");
        }

        [Test]
        public void IsStable_FalseUntilSecondMarker() {
            var nogoods = new NoGoods();

            Assert.That(nogoods.IsStable(), Is.False, "no markers yet, cannot be stable");

            nogoods.MarkExpansion(level: 0);
            Assert.That(nogoods.IsStable(), Is.False, "single marker, cannot be stable");

            nogoods.MarkExpansion(level: 0);
            Assert.That(nogoods.IsStable(), Is.True,
                "two consecutive markers with identical nogood count = stable");
        }

        [Test]
        public void IsStable_FalseWhenNogoodsGrowBetweenMarkers() {
            var nogoods = new NoGoods();
            nogoods.MarkExpansion(level: 0);

            nogoods.Add(level: 0, StateOf("Have(Apple)"));
            nogoods.MarkExpansion(level: 0);

            Assert.That(nogoods.IsStable(), Is.False,
                "a new nogood was added → not stable yet");
        }

        // MarkExpansion watches ONE level (where the graph levelled off): nogoods recorded at
        // other levels — e.g. the goal set keyed under each new top level — must not defeat
        // stability detection, or unsolvable problems loop forever.
        [Test]
        public void IsStable_IgnoresNogoodsAtOtherLevels() {
            var nogoods = new NoGoods();
            nogoods.MarkExpansion(level: 2);

            nogoods.Add(level: 5, StateOf("Have(Apple)"));
            nogoods.MarkExpansion(level: 2);

            Assert.That(nogoods.IsStable(), Is.True,
                "growth at level 5 is irrelevant when watching level 2");
        }

        private static GpBeliefState StateOf(params string[] literals) {
            var state = new GpBeliefState();
            foreach (var s in Factory.StringToSentence(new System.Collections.Generic.List<string>(literals))) {
                state.TryAdd(new GpLiteralNode((ISentence)s));
            }
            return state;
        }
    }
}
