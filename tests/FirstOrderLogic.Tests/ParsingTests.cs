using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ParsingTests : TestBase {
        [Test]
        public void Predicate_PreservesTermOrder() {
            var p = (Predicate)Logic.TryParse("P(a,b,c)");
            Assert.That(p.Terms[0].TermSymbol, Is.EqualTo("a"));
            Assert.That(p.Terms[1].TermSymbol, Is.EqualTo("b"));
            Assert.That(p.Terms[2].TermSymbol, Is.EqualTo("c"));
        }

        [Test]
        public void Terms_VariableVsConstantClassification() {
            var p = (Predicate)Logic.TryParse("P(x,a)");
            Assert.That(p.Terms[0], Is.InstanceOf<Variable>());
            Assert.That(p.Terms[1], Is.InstanceOf<Constant>());
        }

        [Test]
        public void Terms_NestedFunctionParsesAsFunction() {
            var p = (Predicate)Logic.TryParse("P(f(x),a)");
            Assert.That(p.Terms[0], Is.InstanceOf<Function>());
            var f = (Function)p.Terms[0];
            Assert.That(f.TermSymbol, Is.EqualTo("f"));
            Assert.That(f.Arity, Is.EqualTo(1));
        }

        [Test]
        public void Operators_AsciiAliasesMatchWordForms() {
            Assert.That(S("A && B"), Is.EqualTo(S("A AND B")));
            Assert.That(S("A || B"), Is.EqualTo(S("A OR B")));
            Assert.That(S("A => B"), Is.EqualTo(S("A IMPLIES B")));
            Assert.That(S("A <=> B"), Is.EqualTo(S("A IFF B")));
        }

        [Test]
        public void Negation_AllAliasesMatch() {
            var expected = S("NOT A");
            Assert.That(S("!A"), Is.EqualTo(expected));
            Assert.That(S("~A"), Is.EqualTo(expected));
            Assert.That(S("\u00acA"), Is.EqualTo(expected)); // ¬
        }

        // ToString() emits math glyphs (∧ ∨ ⇒ ⇔ ¬ ∀ ∃ ⊤ ⊥); the lexer accepts them too, so
        // parse(print(s)) round-trips for every operator.
        [Test]
        public void RoundTrip_ParsePrintParseIsStable() {
            var original = S("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))");
            Assert.That(S(original.ToString()!), Is.EqualTo(original));
        }

        [Test]
        public void RoundTrip_AllOperators() {
            foreach (var input in new[] {
                "A AND B", "A OR B", "A => B", "A <=> B", "NOT A",
                "FORALL x P(x)", "EXISTS x P(x)", "TRUE", "FALSE",
            }) {
                var parsed = S(input);
                Assert.That(S(parsed.ToString()!), Is.EqualTo(parsed), $"round trip failed for: {input}");
            }
        }

        [Test]
        public void RoundTrip_UnicodeNegation() {
            var input = "\u00ac At(Work)"; // ¬ At(Work)
            var parsed = S(input);
            var reparsed = S(parsed.ToString()!);
            Assert.That(reparsed.ToString(), Is.EqualTo(parsed.ToString()));
        }

        [Test]
        public void Booleans_ParseAsConstants() {
            Assert.That(((Proposition)Logic.TryParse("TRUE")).Tautology, Is.True);
            Assert.That(((Proposition)Logic.TryParse("FALSE")).Contradiction, Is.True);
        }
    }
}
