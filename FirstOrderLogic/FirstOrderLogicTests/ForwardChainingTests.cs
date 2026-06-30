using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class ForwardChainingTests : TestBase {
        private bool Entails(params string[] kbThenGoal) {
            var goal = S(kbThenGoal.Last());
            var kb = Set(kbThenGoal.Take(kbThenGoal.Length - 1).ToArray());
            return ForwardChaining.Entails(kb, goal);
        }

        [Test]
        public void PropositionalModusPonens() {
            Assert.That(Entails("A", "A => B", "B"), Is.True);
        }

        // The canonical first-order example: a fact plus a universally-quantified rule.
        [Test]
        public void FirstOrder_SocratesIsMortal() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Sokrates)"), Is.True);
        }

        [Test]
        public void FirstOrder_UnrelatedConstant_NotEntailed() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Platon)"), Is.False);
        }

        // Transitive chain through two rules — the fixpoint must keep firing past the first round.
        [Test]
        public void Chains_ThroughMultipleRules() {
            Assert.That(Entails("P(a)", "P(x) => Q(x)", "Q(x) => R(x)", "R(a)"), Is.True);
        }

        // A conjunctive body: both premises must be satisfied by the same binding.
        [Test]
        public void ConjunctiveBody_BothPremisesMustHold() {
            Assert.That(
                Entails("Parent(Tom,Bob)", "Male(Tom)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.True);
        }

        [Test]
        public void ConjunctiveBody_MissingPremise_NotEntailed() {
            // No Male(Tom) fact, so the rule never fires.
            Assert.That(
                Entails("Parent(Tom,Bob)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.False);
        }

        // A query with a free variable is entailed when some derived fact instantiates it.
        [Test]
        public void VariableQuery_EntailedByInstance() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(x)"), Is.True);
        }

        [Test]
        public void NegatedQuery_NotDerivable_NotEntailed() {
            // ¬B is a legal query now, but nothing derives it here.
            Assert.That(Entails("A", "A => B", "NOT B"), Is.False);
        }

        [Test]
        public void Saturate_ReturnsDeductiveClosure() {
            var closure = ForwardChaining.Saturate(
                Set("P(a)", "P(x) => Q(x)", "Q(x) => R(x)"));
            Assert.That(closure, Has.Member(S("P(a)")));
            Assert.That(closure, Has.Member(S("Q(a)")));
            Assert.That(closure, Has.Member(S("R(a)")));
            Assert.That(closure.Count, Is.EqualTo(3));
        }

        // ── Literal clauses: negation as explicit falsehood ──────────────────────

        // A rule with a negated head fires like any other — negative conclusions are derivable.
        [Test]
        public void NegativeHead_Derived() {
            Assert.That(Entails("Penguin(pingu)", "Penguin(z) => NOT Flies(z)", "NOT Flies(pingu)"), Is.True);
        }

        [Test]
        public void Saturate_NegativeHead_InClosure() {
            var closure = ForwardChaining.Saturate(Set(
                "Penguin(pingu)", "Species(pingu,Bird)",
                "Penguin(z) => NOT Flies(z)", "Species(z,Bird) => HasFeathers(z)"));
            Assert.That(closure, Has.Member(S("NOT Flies(pingu)")));
            Assert.That(closure, Has.Member(S("HasFeathers(pingu)")));
            Assert.That(closure.Count, Is.EqualTo(4));
        }

        // A negative premise is satisfied only by an explicitly known negative literal.
        [Test]
        public void NegativePremise_MatchesNegativeFact() {
            Assert.That(
                Entails("NOT Employed(a)", "Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.True);
        }

        // Explicit negation, not negation-as-failure: an *absent* positive fact does not
        // satisfy a negative premise.
        [Test]
        public void NegativePremise_AbsenceIsNotNegation() {
            Assert.That(
                Entails("Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.False);
        }

        // Polarity is part of the match: a positive premise never unifies with a negative fact.
        [Test]
        public void PolarityMismatch_DoesNotFire() {
            Assert.That(Entails("NOT Work(a)", "Work(x) => Tired(x)", "Tired(a)"), Is.False);
        }

        // No ex falso: complementary literals coexist in the closure (a detectable tension,
        // not an explosion).
        [Test]
        public void ComplementaryLiterals_Coexist() {
            var closure = ForwardChaining.Saturate(Set("P(a)", "NOT P(a)"));
            Assert.That(closure, Has.Member(S("P(a)")));
            Assert.That(closure, Has.Member(S("NOT P(a)")));
            Assert.That(closure.Count, Is.EqualTo(2));
        }

        // Holds is a literal-fact lookup; a non-literal query (e.g. a rule-form norm) is a misuse
        // and fails loudly rather than silently — callers must not pass implications.
        [Test]
        public void Holds_NonLiteralQuery_Throws() {
            var facts = Set("Have(mySelf, Money)", "Role(mySelf, Worker)");
            Assert.That(() => ForwardChaining.Holds(facts, S("Role(z, Worker) => Have(z, Money)")),
                Throws.ArgumentException);
        }

        // An unsafe rule — head variable y is not bound by the body — is rejected loudly rather
        // than silently looping: each round would otherwise derive a freshly-renamed Q(y#n) and
        // never reach a fixpoint.
        [Test]
        public void UnsafeRule_IsRejected() {
            Assert.That(
                () => ForwardChaining.Saturate(Set("P(a)", "P(x) => Q(y)")),
                Throws.ArgumentException);
        }
    }
}
