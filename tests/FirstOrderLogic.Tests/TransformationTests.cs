using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class TransformationTests : TestBase {
        private ISentence Transform(Transformations.RewriteRule type, string input) {
            var sentence = S(input);
            Transformations.Transform(type, ref sentence);
            return sentence;
        }

        [Test] public void Simplify_AndTrue() => Assert.That(Transform(Transformations.RewriteRule.SimplifyConstants, "(A AND TRUE)"), Is.EqualTo(S("A")));
        [Test] public void Simplify_OrFalse() => Assert.That(Transform(Transformations.RewriteRule.SimplifyConstants, "(A OR FALSE)"), Is.EqualTo(S("A")));
        [Test] public void Simplify_OrTrue() => Assert.That(Transform(Transformations.RewriteRule.SimplifyConstants, "(A OR TRUE)"), Is.EqualTo(S("TRUE")));
        [Test] public void Simplify_AndFalse() => Assert.That(Transform(Transformations.RewriteRule.SimplifyConstants, "(A AND FALSE)"), Is.EqualTo(S("FALSE")));

        [Test]
        public void DissolveImplication() {
            Assert.That(Transform(Transformations.RewriteRule.DissolveImplication, "A => B"),
                Is.EqualTo(S("(NOT A) OR B")));
        }

        [Test]
        public void DissolveBiconditional() {
            Assert.That(Transform(Transformations.RewriteRule.DissolveBiconditional, "A <=> B"),
                Is.EqualTo(S("(A => B) AND (B => A)")));
        }

        [Test]
        public void PushNegation_DeMorganConjunction() {
            Assert.That(Transform(Transformations.RewriteRule.PushNegation, "NOT (A AND B)"),
                Is.EqualTo(S("(NOT A) OR (NOT B)")));
        }

        [Test]
        public void PushNegation_DeMorganDisjunction() {
            Assert.That(Transform(Transformations.RewriteRule.PushNegation, "NOT (A OR B)"),
                Is.EqualTo(S("(NOT A) AND (NOT B)")));
        }

        [Test]
        public void PushNegation_OverUniversal() {
            Assert.That(Transform(Transformations.RewriteRule.PushNegation, "NOT (FORALL x P(x))"),
                Is.EqualTo(S("EXISTS x (NOT P(x))")));
        }

        [Test]
        public void PushNegation_OverExistential() {
            Assert.That(Transform(Transformations.RewriteRule.PushNegation, "NOT (EXISTS x P(x))"),
                Is.EqualTo(S("FORALL x (NOT P(x))")));
        }

        [Test]
        public void DoubleNegation_Collapses() {
            Assert.That(Transform(Transformations.RewriteRule.DoubleNegation, "NOT (NOT A)"),
                Is.EqualTo(S("A")));
        }

        [Test]
        public void Absorption_AndOverOr() {
            Assert.That(Transform(Transformations.RewriteRule.Absorption, "A AND (A OR B)"), Is.EqualTo(S("A")));
        }

        [Test]
        public void Absorption_OrOverAnd() {
            Assert.That(Transform(Transformations.RewriteRule.Absorption, "(A OR B) AND A"), Is.EqualTo(S("A")));
        }

        [Test]
        public void AssociationIdem_NestedDuplicate() {
            Assert.That(Transform(Transformations.RewriteRule.AssociationAndIdem, "A AND (B AND A)"),
                Is.EqualTo(S("B AND A")));
        }

        [Test]
        public void AssociationIdem_FlatDuplicate() {
            Assert.That(Transform(Transformations.RewriteRule.AssociationAndIdem, "A AND A"), Is.EqualTo(S("A")));
        }

        [Test]
        public void DistributionOfDisjunction_RightConjunction() {
            Assert.That(Transform(Transformations.RewriteRule.DistributionOfDisjunction, "A OR (B AND C)"),
                Is.EqualTo(S("(A OR B) AND (A OR C)")));
        }

        [Test]
        public void DistributionOfDisjunction_LeftConjunction() {
            Assert.That(Transform(Transformations.RewriteRule.DistributionOfDisjunction, "(A AND B) OR C"),
                Is.EqualTo(S("(A OR C) AND (B OR C)")));
        }

        [Test]
        public void RemoveDuplicateQuantifier() {
            Assert.That(Transform(Transformations.RewriteRule.RemoveDuplicateQuantifier, "FORALL x (FORALL x P(x))"),
                Is.EqualTo(S("FORALL x P(x)")));
        }

        [Test]
        public void RemoveQuantifier_DropsPrefix() {
            Assert.That(Transform(Transformations.RewriteRule.RemoveQuantifier, "FORALL x P(x)"),
                Is.EqualTo(S("P(x)")));
        }

        [Test]
        public void PullQuantifier_OutOfConjunction() {
            var pnf = S("(FORALL x P(x)) AND Q(a)").ToPrenexForm(out _);
            Assert.That(pnf, Is.EqualTo(S("FORALL x (P(x) AND Q(a))")));
        }

        [Test]
        public void PushNegation_FiresUnderConjunction() {
            Assert.That(Transform(Transformations.RewriteRule.PushNegation, "P AND (NOT (A AND B))"),
                Is.EqualTo(S("P AND ((NOT A) OR (NOT B))")));
        }

        [Test]
        public void DissolveImplication_FiresUnderDisjunction() {
            Assert.That(Transform(Transformations.RewriteRule.DissolveImplication, "R OR (A => B)"),
                Is.EqualTo(S("R OR ((NOT A) OR B)")));
        }

        [Test]
        public void DoubleNegation_FiresTwoLevelsDeep() {
            Assert.That(Transform(Transformations.RewriteRule.DoubleNegation, "Q AND (P AND (NOT (NOT A)))"),
                Is.EqualTo(S("Q AND (P AND A)")));
        }

        [Test]
        public void Distribution_FiresUnderConjunction() {
            Assert.That(Transform(Transformations.RewriteRule.DistributionOfDisjunction, "Z AND (A OR (B AND C))"),
                Is.EqualTo(S("Z AND ((A OR B) AND (A OR C))")));
        }

        [Test]
        public void Cnf_NestedMixedFormula() {
            var cnf = S("(P => Q) AND (R OR (S AND T))").ToConjunctiveNormalForm(out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }
    }
}
