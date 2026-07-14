using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ParsingTests : TestBase {
        [Test]
        public void Predicate_PreservesTermOrder() {
            var p = (Predicate)Logic.Parse("P(a,b,c)");
            Assert.That(p.Terms[0].TermSymbol, Is.EqualTo("a"));
            Assert.That(p.Terms[1].TermSymbol, Is.EqualTo("b"));
            Assert.That(p.Terms[2].TermSymbol, Is.EqualTo("c"));
        }

        [Test]
        public void Terms_VariableVsConstantClassification() {
            var p = (Predicate)Logic.Parse("P(x,a)");
            Assert.That(p.Terms[0], Is.InstanceOf<Variable>());
            Assert.That(p.Terms[1], Is.InstanceOf<Constant>());
        }

        [Test]
        public void Terms_NestedFunctionParsesAsFunction() {
            var p = (Predicate)Logic.Parse("P(f(x),a)");
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
            Assert.That(((Proposition)Logic.Parse("TRUE")).Tautology, Is.True);
            Assert.That(((Proposition)Logic.Parse("FALSE")).Contradiction, Is.True);
        }

        // A quantifier body is a UnarySentence, so it stops at the first binary connective: the
        // binder scopes over P(x) alone, not the conjunction. BindConstantsToVariable rewrites
        // exactly that extent, so a shift here silently changes what every quantified input means.
        [Test]
        public void Quantifier_BodyStopsAtTheFirstBinaryConnective() {
            var s = (IComplexSentence)S("FORALL x P(x) AND Q(x)");
            Assert.That(s.Connective.Symbol, Is.EqualTo(Connective.LogicSymbol.CONJUNCTION),
                $"root of 'FORALL x P(x) AND Q(x)' is {s.Connective}, so the binder's extent moved");
            Assert.That(s, Is.EqualTo(S("(FORALL x P(x)) AND Q(x)")));
            Assert.That(s, Is.Not.EqualTo(S("FORALL x (P(x) AND Q(x))")));
        }

        [Test]
        public void Quantifier_ParenthesisedBodyTakesTheWholeConjunction() {
            var s = (IComplexSentence)S("FORALL x (P(x) AND Q(x))");
            Assert.That(s.IsQuantifier, Is.True);
        }

        [Test]
        public void Implication_IsRightAssociative() =>
            Assert.That(S("A => B => C"), Is.EqualTo(S("A => (B => C)")),
                "IMPLIES is right-associative; (A => B) => C is a different sentence");

        [Test]
        public void Disjunction_IsLeftAssociative() {
            var s = (IComplexSentence)S("A OR B OR C");
            Assert.That(s.Children[0], Is.EqualTo(S("A OR B")),
                "OR is left-associative, so the left child carries the nesting");
            Assert.That(s, Is.EqualTo(S("(A OR B) OR C")));
        }

        [Test]
        public void Conjunction_IsLeftAssociative() =>
            Assert.That(S("A AND B AND C"), Is.EqualTo(S("(A AND B) AND C")));
    }
}
