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
    }
}
