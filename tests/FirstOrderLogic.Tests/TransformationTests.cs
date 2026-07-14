using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class TransformationTests : TestBase {
        private ISentence Transform(TransformationFOL.EquivType type, string input) {
            var sentence = S(input);
            TransformationFOL.Transform(type, ref sentence);
            return sentence;
        }

        // ── Constant folding ──────────────────────────────────────────────────────
        [Test] public void Simplify_AndTrue() => Assert.That(Transform(TransformationFOL.EquivType.SimplifyConstants, "(A AND TRUE)"), Is.EqualTo(S("A")));
        [Test] public void Simplify_OrFalse() => Assert.That(Transform(TransformationFOL.EquivType.SimplifyConstants, "(A OR FALSE)"), Is.EqualTo(S("A")));
        [Test] public void Simplify_OrTrue() => Assert.That(Transform(TransformationFOL.EquivType.SimplifyConstants, "(A OR TRUE)"), Is.EqualTo(S("TRUE")));
        [Test] public void Simplify_AndFalse() => Assert.That(Transform(TransformationFOL.EquivType.SimplifyConstants, "(A AND FALSE)"), Is.EqualTo(S("FALSE")));

        // ── Connective elimination ──────────────────────────────────────────────────
        [Test]
        public void DissolveImplication() {
            Assert.That(Transform(TransformationFOL.EquivType.DissolveImplication, "A => B"),
                Is.EqualTo(S("(NOT A) OR B")));
        }

        [Test]
        public void DissolveBiconditional() {
            Assert.That(Transform(TransformationFOL.EquivType.DissolveBiconditional, "A <=> B"),
                Is.EqualTo(S("(A => B) AND (B => A)")));
        }

        // ── Negation ────────────────────────────────────────────────────────────────
        [Test]
        public void PushNegation_DeMorganConjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.PushNegation, "NOT (A AND B)"),
                Is.EqualTo(S("(NOT A) OR (NOT B)")));
        }

        [Test]
        public void PushNegation_DeMorganDisjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.PushNegation, "NOT (A OR B)"),
                Is.EqualTo(S("(NOT A) AND (NOT B)")));
        }

        [Test]
        public void PushNegation_OverUniversal() {
            Assert.That(Transform(TransformationFOL.EquivType.PushNegation, "NOT (FORALL x P(x))"),
                Is.EqualTo(S("EXISTS x (NOT P(x))")));
        }

        [Test]
        public void PushNegation_OverExistential() {
            Assert.That(Transform(TransformationFOL.EquivType.PushNegation, "NOT (EXISTS x P(x))"),
                Is.EqualTo(S("FORALL x (NOT P(x))")));
        }

        [Test]
        public void DoubleNegation_Collapses() {
            Assert.That(Transform(TransformationFOL.EquivType.DoubleNegation, "NOT (NOT A)"),
                Is.EqualTo(S("A")));
        }

        // ── Absorption & idempotency ──────────────────────────────────────────────────
        [Test]
        public void Absorption_AndOverOr() {
            Assert.That(Transform(TransformationFOL.EquivType.Absorption, "A AND (A OR B)"), Is.EqualTo(S("A")));
        }

        [Test]
        public void Absorption_OrOverAnd() {
            Assert.That(Transform(TransformationFOL.EquivType.Absorption, "(A OR B) AND A"), Is.EqualTo(S("A")));
        }

        [Test]
        public void AssociationIdem_NestedDuplicate() {
            Assert.That(Transform(TransformationFOL.EquivType.AssociationAndIdem, "A AND (B AND A)"),
                Is.EqualTo(S("B AND A")));
        }

        // Plain idempotency: A AND A ≡ A (called out in the implementation's own comment).
        [Test]
        public void AssociationIdem_FlatDuplicate() {
            Assert.That(Transform(TransformationFOL.EquivType.AssociationAndIdem, "A AND A"), Is.EqualTo(S("A")));
        }

        // ── Distribution ──────────────────────────────────────────────────────────────
        [Test]
        public void DistributionOfDisjunction_RightConjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.DistributionOfDisjunction, "A OR (B AND C)"),
                Is.EqualTo(S("(A OR B) AND (A OR C)")));
        }

        [Test]
        public void DistributionOfDisjunction_LeftConjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.DistributionOfDisjunction, "(A AND B) OR C"),
                Is.EqualTo(S("(A OR C) AND (B OR C)")));
        }

        // ── Quantifiers ─────────────────────────────────────────────────────────────────
        [Test]
        public void RemoveDuplicateQuantifier() {
            Assert.That(Transform(TransformationFOL.EquivType.RemoveDuplicateQuantifier, "FORALL x (FORALL x P(x))"),
                Is.EqualTo(S("FORALL x P(x)")));
        }

        [Test]
        public void RemoveQuantifier_DropsPrefix() {
            Assert.That(Transform(TransformationFOL.EquivType.RemoveQuantifier, "FORALL x P(x)"),
                Is.EqualTo(S("P(x)")));
        }

        [Test]
        public void PullQuantifier_OutOfConjunction() {
            var pnf = Logic.ToPrenexForm(S("(FORALL x P(x)) AND Q(a)"), out _);
            Assert.That(pnf, Is.EqualTo(S("FORALL x (P(x) AND Q(a))")));
        }

        // ── Nested reassembly: a rewrite fires below the root and must be spliced back ──
        [Test]
        public void PushNegation_FiresUnderConjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.PushNegation, "P AND (NOT (A AND B))"),
                Is.EqualTo(S("P AND ((NOT A) OR (NOT B))")));
        }

        [Test]
        public void DissolveImplication_FiresUnderDisjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.DissolveImplication, "R OR (A => B)"),
                Is.EqualTo(S("R OR ((NOT A) OR B)")));
        }

        [Test]
        public void DoubleNegation_FiresTwoLevelsDeep() {
            Assert.That(Transform(TransformationFOL.EquivType.DoubleNegation, "Q AND (P AND (NOT (NOT A)))"),
                Is.EqualTo(S("Q AND (P AND A)")));
        }

        [Test]
        public void Distribution_FiresUnderConjunction() {
            Assert.That(Transform(TransformationFOL.EquivType.DistributionOfDisjunction, "Z AND (A OR (B AND C))"),
                Is.EqualTo(S("Z AND ((A OR B) AND (A OR C))")));
        }

        [Test]
        public void Cnf_NestedMixedFormula() {
            var cnf = Logic.ToConjunctiveNormalForm(S("(P => Q) AND (R OR (S AND T))"), out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }
    }
}
