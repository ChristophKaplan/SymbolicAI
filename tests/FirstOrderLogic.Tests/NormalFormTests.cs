using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class NormalFormTests : TestBase {
        [Test]
        public void Prenex_EliminatesImplication() {
            var pnf = S("(P(x) => Q(y)) AND R(z)").ToPrenexForm(out _);
            Assert.That(pnf, Is.EqualTo(S("((NOT P(x)) OR Q(y)) AND R(z)")));
        }

        [Test]
        public void Cnf_FromImplication() {
            var input = S("P(x) => (P(y) AND Q(z))");
            var cnf = input.ToConjunctiveNormalForm(out _);
            Assert.That(input.IsCNF(), Is.False);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        [Test]
        public void Cnf_DistributesOrOverAnd() {
            var cnf = S("A OR (B AND C)").ToConjunctiveNormalForm(out _);
            Assert.That(cnf, Is.EqualTo(S("(A OR B) AND (A OR C)")));
        }

        [Test]
        public void Cnf_FromBiconditional() {
            var cnf = S("A <=> B").ToConjunctiveNormalForm(out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        [Test]
        public void Cnf_NestedConjunctionsBothSides() {
            var cnf = S("(A AND B) OR (C AND D)").ToConjunctiveNormalForm(out _);
            Assert.That(cnf.IsCNF(), Is.True);
        }

        // Skolem names come from a process-wide counter; reset so the pinned names below
        // stay deterministic regardless of test order.
        [SetUp]
        public void ResetSkolemNames() => FirstOrderLogicExtensions.ResetSkolemCounter();

        // Witness names ("sk$1", …) are unparseable by design, so expected sentences are built
        // by substituting placeholder constants in a parsed template.
        private static Function Sk(int n, params string[] universals) =>
            new($"sk${n}", universals.Select(u => (Term)new Variable(u)).ToArray());

        [Test]
        public void Skolem_ExistentialBeforeUniversal_UsesConstant() {
            Assert.That(S("EXISTS x (FORALL y P(x,y))").SkolemForm(),
                Is.EqualTo(S("P(k1,y)").Substitute(new Constant("k1"), Sk(1))));
        }

        [Test]
        public void Skolem_UniversalBeforeExistential_UsesFunction() {
            Assert.That(S("FORALL x (EXISTS y P(x,y))").SkolemForm(),
                Is.EqualTo(S("P(x,k1)").Substitute(new Constant("k1"), Sk(1, "x"))));
        }

        [Test]
        public void Skolem_DistinctExistentialsGetDistinctSymbols() {
            Assert.That(S("EXISTS x (EXISTS y (P(x) AND Q(y)))").SkolemForm(),
                Is.EqualTo(S("(P(k1) AND Q(k2))")
                    .Substitute(new Constant("k1"), Sk(1))
                    .Substitute(new Constant("k2"), Sk(2))));
        }

        [Test]
        public void Skolem_MixedPrefixTracksScope() {
            Assert.That(S("FORALL x (EXISTS y (FORALL z (EXISTS w R(x,y,z,w))))").SkolemForm(),
                Is.EqualTo(S("R(x,k1,z,k2)")
                    .Substitute(new Constant("k1"), Sk(1, "x"))
                    .Substitute(new Constant("k2"), Sk(2, "x", "z"))));
        }

        [Test]
        public void ClauseSet_SplitsTopLevelConjuncts() {
            var pnf = S("(P(x) => Q(y)) AND R(z)").ToPrenexForm(out _);
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
