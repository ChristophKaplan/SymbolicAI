using System;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ClauseTests : TestBase {
        // A non-literal must throw: silently producing an empty literal list would make the
        // clause indistinguishable from the empty clause, i.e. from a successful refutation.
        [Test]
        public void Constructor_RejectsNonLiterals() {
            Assert.Throws<ArgumentException>(() => new Clause(S("A AND B")));
            Assert.Throws<ArgumentException>(() => new Clause(S("P(x)"), S("A => B")));
        }

        [Test]
        public void Constructor_DedupsLiterals() {
            var clause = new Clause(S("P(a)"), S("P(a)"));
            Assert.That(clause.Literals.Count, Is.EqualTo(1));
            Assert.That(clause.Literals[0], Is.EqualTo(S("P(a)")));
        }

        [Test]
        public void GetClauseSet_RejectsNafSentences() {
            Assert.Throws<ArgumentException>(() => S("(NAF P(a)) AND Q(a)").GetClauseSet());
            Assert.Throws<ArgumentException>(() => S("NAF P(a)").GetClauseSet());
        }
    }
}
