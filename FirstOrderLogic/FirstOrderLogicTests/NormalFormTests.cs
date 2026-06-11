using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class NormalFormTests : TestBase {
        // ── Prenex ────────────────────────────────────────────────────────────────
        [Test]
        public void Prenex_EliminatesImplication() {
            var pnf = Logic.ToPrenexForm(S("(P(x) => Q(y)) AND R(z)"), out _);
            Assert.That(pnf, Is.EqualTo(S("((NOT P(x)) OR Q(y)) AND R(z)")));
        }

        // ── CNF ──────────────────────────────────────────────────────────────────
        [Test]
        public void Cnf_FromImplication() {
            var input = S("P(x) => (P(y) AND Q(z))");
            var cnf = Logic.ToConjunctiveNormalForm(input, out _);
            Assert.That(input.IsCNF(), Is.False);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        [Test]
        public void Cnf_DistributesOrOverAnd() {
            var cnf = Logic.ToConjunctiveNormalForm(S("A OR (B AND C)"), out _);
            Assert.That(cnf, Is.EqualTo(S("(A OR B) AND (A OR C)")));
        }

        [Test]
        public void Cnf_FromBiconditional() {
            var cnf = Logic.ToConjunctiveNormalForm(S("A <=> B"), out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        [Test]
        public void Cnf_NestedConjunctionsBothSides() {
            var cnf = Logic.ToConjunctiveNormalForm(S("(A AND B) OR (C AND D)"), out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        // ── Skolemization ──────────────────────────────────────────────────────────
        [Test]
        public void Skolem_ExistentialBeforeUniversal_UsesConstant() {
            Assert.That(Logic.SkolemForm(S("EXISTS x (FORALL y P(x,y))")), Is.EqualTo(S("P(sk1,y)")));
        }

        [Test]
        public void Skolem_UniversalBeforeExistential_UsesFunction() {
            Assert.That(Logic.SkolemForm(S("FORALL x (EXISTS y P(x,y))")), Is.EqualTo(S("P(x,sk1(x))")));
        }

        [Test]
        public void Skolem_DistinctExistentialsGetDistinctSymbols() {
            Assert.That(Logic.SkolemForm(S("EXISTS x (EXISTS y (P(x) AND Q(y)))")),
                Is.EqualTo(S("(P(sk1) AND Q(sk2))")));
        }

        // Mixed prefix ∀x ∃y ∀z ∃w: y depends on x, w depends on x and z.
        [Test]
        public void Skolem_MixedPrefixTracksScope() {
            Assert.That(Logic.SkolemForm(S("FORALL x (EXISTS y (FORALL z (EXISTS w R(x,y,z,w))))")),
                Is.EqualTo(S("R(x,sk1(x),z,sk2(x,z))")));
        }

        // ── Clause sets ──────────────────────────────────────────────────────────
        [Test]
        public void ClauseSet_SplitsTopLevelConjuncts() {
            var pnf = Logic.ToPrenexForm(S("(P(x) => Q(y)) AND R(z)"), out _);
            var clauses = pnf.GetClauseSet();
            Assert.That(clauses.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClauseSet_ThrowsForNonCnf() {
            Assert.That(() => S("P(x) => Q(x)").GetClauseSet(),
                Throws.Exception);
        }
    }
}
