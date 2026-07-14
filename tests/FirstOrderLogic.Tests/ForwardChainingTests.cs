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

        [Test]
        public void FirstOrder_SocratesIsMortal() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Sokrates)"), Is.True);
        }

        [Test]
        public void FirstOrder_UnrelatedConstant_NotEntailed() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(Platon)"), Is.False);
        }

        [Test]
        public void Chains_ThroughMultipleRules() {
            Assert.That(Entails("P(a)", "P(x) => Q(x)", "Q(x) => R(x)", "R(a)"), Is.True);
        }

        [Test]
        public void ConjunctiveBody_BothPremisesMustHold() {
            Assert.That(
                Entails("Parent(Tom,Bob)", "Male(Tom)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.True);
        }

        [Test]
        public void ConjunctiveBody_MissingPremise_NotEntailed() {
            Assert.That(
                Entails("Parent(Tom,Bob)",
                        "(Parent(x,y) AND Male(x)) => Father(x,y)", "Father(Tom,Bob)"),
                Is.False);
        }

        [Test]
        public void VariableQuery_EntailedByInstance() {
            Assert.That(Entails("Human(Sokrates)", "Human(x) => Mortal(x)", "Mortal(x)"), Is.True);
        }

        [Test]
        public void NegatedQuery_NotDerivable_NotEntailed() {
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

        [Test]
        public void NegativePremise_MatchesNegativeFact() {
            Assert.That(
                Entails("NOT Employed(a)", "Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.True);
        }

        [Test]
        public void NegativePremise_AbsenceIsNotNegation() {
            Assert.That(
                Entails("Subject(a)",
                        "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)", "NeedsJob(a)"),
                Is.False);
        }

        [Test]
        public void PolarityMismatch_DoesNotFire() {
            Assert.That(Entails("NOT Work(a)", "Work(x) => Tired(x)", "Tired(a)"), Is.False);
        }

        [Test]
        public void ComplementaryLiterals_Coexist() {
            var closure = ForwardChaining.Saturate(Set("P(a)", "NOT P(a)"));
            Assert.That(closure, Has.Member(S("P(a)")));
            Assert.That(closure, Has.Member(S("NOT P(a)")));
            Assert.That(closure.Count, Is.EqualTo(2));
        }

        [Test]
        public void Holds_NonLiteralQuery_Throws() {
            var facts = Set("Have(mySelf, Money)", "Role(mySelf, Worker)");
            Assert.That(() => ForwardChaining.Holds(facts, S("Role(z, Worker) => Have(z, Money)")),
                Throws.ArgumentException);
        }

        [Test]
        public void UnsafeRule_IsRejected() {
            Assert.That(
                () => ForwardChaining.Saturate(Set("P(a)", "P(x) => Q(y)")),
                Throws.ArgumentException);
        }

        [Test]
        public void IsChainable_AcceptsFactsAndRules() {
            Assert.That(Rule.IsChainable(S("Human(Sokrates)")), Is.True);
            Assert.That(Rule.IsChainable(S("NOT Flies(pingu)")), Is.True);
            Assert.That(Rule.IsChainable(S("Human(x) => Mortal(x)")), Is.True);
            Assert.That(Rule.IsChainable(S("(Bird(x) AND NOT Penguin(x)) => Flies(x)")), Is.True);
            Assert.That(Rule.IsChainable(S("FORALL x (Human(x) => Mortal(x))")), Is.True);
        }

        [Test]
        public void IsChainable_RejectsRicherForms() {
            Assert.That(Rule.IsChainable(S("(EXISTS y (Parent(Tom, y))) => IsParent(Tom)")), Is.False);
            Assert.That(Rule.IsChainable(S("Rain(x) OR Snow(x)")), Is.False);
            Assert.That(Rule.IsChainable(S("Human(x) => (Mortal(x) AND Alive(x))")), Is.False);
        }

        [Test]
        public void Naf_DerivesFromAbsence() {
            Assert.That(
                Entails("Bird(tweety)",
                        "(Bird(x) AND NAF Penguin(x)) => Flies(x)",
                        "Flies(tweety)"),
                Is.True);
        }

        [Test]
        public void Naf_BlockedByDerivedInstance() {
            Assert.That(
                Entails("Bird(tweety)", "Antarctic(tweety)",
                        "Antarctic(x) => Penguin(x)",
                        "(Bird(x) AND NAF Penguin(x)) => Flies(x)",
                        "Flies(tweety)"),
                Is.False);
        }

        [Test]
        public void Naf_FreeVariable_MeansNoInstance() {
            Assert.That(
                Entails("Person(Tom)",
                        "(Person(x) AND NAF Parent(x, y)) => Childless(x)",
                        "Childless(Tom)"),
                Is.True);
            Assert.That(
                Entails("Person(Tom)", "Parent(Tom, Bob)",
                        "(Person(x) AND NAF Parent(x, y)) => Childless(x)",
                        "Childless(Tom)"),
                Is.False);
        }

        [Test]
        public void Naf_ClosedWorldBridgeRule() {
            var closure = ForwardChaining.Saturate(Set(
                "Course(math)", "Course(art)", "Enrolled(Tom, math)",
                "(Course(y) AND NAF Enrolled(Tom, y)) => NOT Enrolled(Tom, y)"));
            Assert.That(closure, Has.Member(S("NOT Enrolled(Tom, art)")));
            Assert.That(closure, Has.No.Member(S("NOT Enrolled(Tom, math)")));
        }

        // NAF binds loosely like NOT: a bare NAF antecedent needs the parentheses,
        // else NAF swallows the whole implication.
        [Test]
        public void Naf_StratifiedOrder() {
            var closure = ForwardChaining.Saturate(Set(
                "(NAF P(a)) => Q(a)",
                "(NAF Q(a)) => R(a)"));
            Assert.That(closure, Has.Member(S("Q(a)")));
            Assert.That(closure, Has.No.Member(S("R(a)")));
        }

        [Test]
        public void Naf_CycleIsRejected() {
            Assert.That(
                () => ForwardChaining.Saturate(Set("(NAF P(a)) => Q(a)", "(NAF Q(a)) => P(a)")),
                Throws.ArgumentException);
        }

        [Test]
        public void Naf_CannotBindHeadVariable() {
            Assert.That(
                () => ForwardChaining.Saturate(Set("(NAF P(x)) => Q(x)")),
                Throws.ArgumentException);
        }

        [Test]
        public void Naf_RulesAreChainable() {
            Assert.That(Rule.IsChainable(S("(Bird(x) AND NAF Penguin(x)) => Flies(x)")), Is.True);
        }

        [Test]
        public void Naf_RejectedByCnf() {
            Assert.That(
                () => S("(Bird(x) AND NAF Penguin(x)) => Flies(x)").ToConjunctiveNormalForm(),
                Throws.ArgumentException);
        }
    }
}
