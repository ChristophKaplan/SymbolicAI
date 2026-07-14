using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // Guards the set-based-equality fixes and the GpLiteralNode identity-hash bug
    // (identity hash + literal-based Equals broke the HashSet contract).
    [TestFixture]
    public class EqualityAndHashTests : PlanningTestBase {
        [Test]
        public void GpLiteralNode_HashCode_IsConsistentWithEquals() {
            var literalA = L("Have(Cake)");
            var literalB = L("Have(Cake)");

            var nodeA = new GpLiteralNode(literalA);
            var nodeB = new GpLiteralNode(literalB);

            Assert.That(nodeA, Is.EqualTo(nodeB), "literal-equal nodes must compare Equal");
            Assert.That(nodeA.GetHashCode(), Is.EqualTo(nodeB.GetHashCode()),
                "Equal nodes MUST have equal hash codes (HashSet contract)");

            var set = new HashSet<GpLiteralNode> { nodeA, nodeB };
            Assert.That(set, Has.Count.EqualTo(1));
        }

        [Test]
        public void GpAction_Equality_IsMultisetSensitive() {
            var p = L("P(Obj)");
            var q = L("Q(Obj)");
            var eff = L("R(Obj)");

            var ppq = new GpAction("A", new() { p, p, q }, new() { eff });
            var pqq = new GpAction("A", new() { p, q, q }, new() { eff });

            Assert.That(ppq, Is.Not.EqualTo(pqq),
                "pre={P,P,Q} and pre={P,Q,Q} have the same size and mutual containment but " +
                "different element counts — multiset equality must distinguish them");
        }

        [Test]
        public void GpAction_Equality_DuplicatesInDifferentOrder_AreEqualWithEqualHashes() {
            var p = L("P(Obj)");
            var q = L("Q(Obj)");
            var eff = L("R(Obj)");

            var a = new GpAction("A", new() { p, p, q }, new() { eff });
            var b = new GpAction("A", new() { q, p, p }, new() { eff });

            Assert.That(a, Is.EqualTo(b), "same multiset in different order must be Equal");
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()),
                "equal actions MUST have equal hash codes");
        }

        [Test]
        public void GpAction_HashCode_DuplicatePairsDoNotCancel() {
            var p = L("P(Obj)");
            var q = L("Q(Obj)");
            var eff = L("R(Obj)");

            var pp = new GpAction("A", new() { p, p }, new() { eff });
            var qq = new GpAction("A", new() { q, q }, new() { eff });

            Assert.That(pp, Is.Not.EqualTo(qq));
            Assert.That(pp.GetHashCode(), Is.Not.EqualTo(qq.GetHashCode()),
                "duplicate literals must contribute to the hash instead of XOR-cancelling");
        }

        [Test]
        public void GpAction_Equality_DistinguishesDifferentEffects() {
            var pre = L("At(z, Work)");
            var eff1 = L("Have(Money)");
            var eff2 = L("Have(Cake)");

            var a = new GpAction("Work", new() { pre }, new() { eff1 });
            var b = new GpAction("Work", new() { pre }, new() { eff2 });

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void GpBeliefState_Equality_IsOrderInsensitive() {
            var lit1 = L("Have(Apple)");
            var lit2 = L("Subject(Subject1)");

            var stateAB = new GpBeliefState();
            stateAB.TryAdd(new GpLiteralNode(lit1));
            stateAB.TryAdd(new GpLiteralNode(lit2));

            var stateBA = new GpBeliefState();
            stateBA.TryAdd(new GpLiteralNode(lit2));
            stateBA.TryAdd(new GpLiteralNode(lit1));

            Assert.That(stateAB, Is.EqualTo(stateBA),
                "belief states with the same literals in different insertion order must be Equal");
            Assert.That(stateAB.GetHashCode(), Is.EqualTo(stateBA.GetHashCode()));
        }

        [Test]
        public void GpBeliefState_Add_ReturnsCanonicalNode() {
            var literal = L("Have(Apple)");
            var first = new GpLiteralNode(literal);
            var duplicate = new GpLiteralNode(literal);

            var state = new GpBeliefState();
            var canonical1 = state.Add(first);
            var canonical2 = state.Add(duplicate);

            Assert.That(canonical1, Is.SameAs(first), "first add stores the new instance");
            Assert.That(canonical2, Is.SameAs(first),
                "second add of an Equal literal must return the original instance, not the new one");
            Assert.That(state.GetLiteralNodes, Has.Count.EqualTo(1));
        }

        [Test]
        public void GpActionSet_Equality_IsOrderInsensitive() {
            var actionA = MakeAction("A");
            var actionB = MakeAction("B");

            var setAB = new GpActionSet();
            setAB.TryAdd(new GpActionNode(actionA));
            setAB.TryAdd(new GpActionNode(actionB));

            var setBA = new GpActionSet();
            setBA.TryAdd(new GpActionNode(actionB));
            setBA.TryAdd(new GpActionNode(actionA));

            Assert.That(setAB, Is.EqualTo(setBA));
            Assert.That(setAB.GetHashCode(), Is.EqualTo(setBA.GetHashCode()));
        }

        [Test]
        public void GpActionSet_DistinctActions_AreNotEqual() {
            var setA = new GpActionSet();
            setA.TryAdd(new GpActionNode(MakeAction("A")));
            var setB = new GpActionSet();
            setB.TryAdd(new GpActionNode(MakeAction("B")));

            Assert.That(setA, Is.Not.EqualTo(setB));
        }

        private static GpAction MakeAction(string name) {
            return Factory.Create(
                name,
                new() { "Subject(z)" },
                new() { "Subject(z)" });
        }
    }
}
