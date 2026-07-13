using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // Direct tests for every mutex type on small hand-built nodes, verified against the
    // Blum & Furst definitions:
    //   Inconsistent effects — an effect of one action negates an effect of the other.
    //   Interference         — an effect of one action negates a precondition of the other.
    //   Competing needs      — a precondition of one is mutex (previous level) with a
    //                          precondition of the other.
    //   Literal negation     — the two literals are negations of each other.
    //   Inconsistent support — every pair of actions supporting the two literals is mutex.
    [TestFixture]
    public class MutexTests : PlanningTestBase {
        [Test]
        public void InconsistentEffects_WhenEffectsNegateEachOther() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("Q(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("P(Obj)") }, new() { L("-Q(Obj)") }));

            Assert.That(a.IsInconsistentEffects(b), Is.True);
            Assert.That(b.IsInconsistentEffects(a), Is.True, "relation must be symmetric");
            Assert.That(a.IsInterference(b), Is.False,
                "no effect negates a precondition here — only the effects clash");
            Assert.That(a.GetMutexType(b), Is.EqualTo(MutexType.InconsistentEffects));
        }

        [Test]
        public void Interference_WhenEffectNegatesOtherPrecondition() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("-R(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("R(Obj)") }, new() { L("S(Obj)") }));

            Assert.That(a.IsInterference(b), Is.True);
            Assert.That(b.IsInterference(a), Is.True, "interference checks both directions");
            Assert.That(a.IsInconsistentEffects(b), Is.False, "-R(Obj) and S(Obj) do not clash");
            Assert.That(a.GetMutexType(b), Is.EqualTo(MutexType.Interference));
        }

        [Test]
        public void CompetingNeeds_WhenPreconditionsAreMutexAtPreviousLevel() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("X(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("-P(Obj)") }, new() { L("Y(Obj)") }));

            var litP = new GpLiteralNode(L("P(Obj)"));
            var litNotP = new GpLiteralNode(L("-P(Obj)"));
            litP.TryAddMutexRelations(litNotP, MutexType.LiteralNegation);
            litP.ConnectTo(a);
            litNotP.ConnectTo(b);

            Assert.That(a.IsCompetingNeeds(b), Is.True);
            Assert.That(b.IsCompetingNeeds(a), Is.True, "relation must be symmetric");
            Assert.That(a.IsInconsistentEffects(b), Is.False);
            Assert.That(a.IsInterference(b), Is.False);
            Assert.That(a.GetMutexType(b), Is.EqualTo(MutexType.CompetingNeeds));
        }

        [Test]
        public void NoActionMutex_WhenActionsAreIndependent() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("X(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("P(Obj)") }, new() { L("Y(Obj)") }));

            var litP = new GpLiteralNode(L("P(Obj)"));
            litP.ConnectTo(a);
            litP.ConnectTo(b);

            Assert.That(a.GetMutexType(b), Is.EqualTo(MutexType.None));
        }

        [Test]
        public void LiteralNegation_BetweenLiteralAndItsNegation() {
            var lit = new GpLiteralNode(L("P(Obj)"));
            var negLit = new GpLiteralNode(L("-P(Obj)"));

            Assert.That(lit.GetMutexType(negLit), Is.EqualTo(MutexType.LiteralNegation));
            Assert.That(negLit.GetMutexType(lit), Is.EqualTo(MutexType.LiteralNegation));
        }

        [Test]
        public void InconsistentSupport_WhenAllSupporterPairsAreMutex() {
            // A and B have inconsistent effects (M vs -M); each is the ONLY supporter of its
            // literal, so L1 and L2 have inconsistent support.
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("L1(Obj)"), L("-M(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("P(Obj)") }, new() { L("L2(Obj)"), L("M(Obj)") }));

            var actionNodes = new List<GpNode> { a, b };
            actionNodes.CheckMutexRelations();
            Assert.That(a.MutexRelation, Is.Not.Empty, "sanity: the action mutex must be recorded");

            var lit1 = new GpLiteralNode(L("L1(Obj)"));
            var lit2 = new GpLiteralNode(L("L2(Obj)"));
            a.ConnectTo(lit1);
            b.ConnectTo(lit2);

            Assert.That(lit1.IsInconsistentSupport(lit2), Is.True);
            Assert.That(lit2.IsInconsistentSupport(lit1), Is.True, "relation must be symmetric");
            Assert.That(lit1.GetMutexType(lit2), Is.EqualTo(MutexType.InconsistentSupport));
        }

        [Test]
        public void NoInconsistentSupport_WhenOneNonMutexSupporterPairExists() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("L1(Obj)"), L("-M(Obj)") }));
            var b = new GpActionNode(new GpAction("B", new() { L("P(Obj)") }, new() { L("L2(Obj)"), L("M(Obj)") }));
            // C also supports L2 and conflicts with nothing.
            var c = new GpActionNode(new GpAction("C", new() { L("P(Obj)") }, new() { L("L2(Obj)") }));

            var actionNodes = new List<GpNode> { a, b, c };
            actionNodes.CheckMutexRelations();

            var lit1 = new GpLiteralNode(L("L1(Obj)"));
            var lit2 = new GpLiteralNode(L("L2(Obj)"));
            a.ConnectTo(lit1);
            b.ConnectTo(lit2);
            c.ConnectTo(lit2);

            Assert.That(lit1.IsInconsistentSupport(lit2), Is.False,
                "the pair (A, C) is conflict-free, so L1 and L2 can be achieved together");
            Assert.That(lit1.GetMutexType(lit2), Is.EqualTo(MutexType.None));
        }

        [Test]
        public void NoInconsistentSupport_WhenLiteralsShareASupporter() {
            var a = new GpActionNode(new GpAction("A", new() { L("P(Obj)") }, new() { L("L1(Obj)"), L("L2(Obj)") }));

            var lit1 = new GpLiteralNode(L("L1(Obj)"));
            var lit2 = new GpLiteralNode(L("L2(Obj)"));
            a.ConnectTo(lit1);
            a.ConnectTo(lit2);

            Assert.That(lit1.IsInconsistentSupport(lit2), Is.False,
                "one action producing both literals is always a consistent way");
        }
    }
}
