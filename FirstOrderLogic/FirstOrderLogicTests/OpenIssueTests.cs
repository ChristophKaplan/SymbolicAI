using System;
using System.Collections.Generic;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Repro tests for the open findings of the July 2026 whole-project review. Each test
    // asserts the CORRECT behavior, so a failure here confirms the corresponding bug is
    // real and still present. Once a bug is fixed its test doubles as a regression pin.
    [Category("OpenIssue")]
    public class OpenIssueTests : TestBase {
        private static readonly TimeSpan Bound = TimeSpan.FromSeconds(5);

        // Finding 1 — ForwardChaining.Saturate adds facts unrenamed, so a non-ground fact
        // exposes its variable to the unifier and a later premise match silently overwrites
        // the binding, deriving a fact that is NOT entailed.
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoUnsoundDerivation() {
            var kb = Set("P(x,x)", "R(x)", "(P(w,a) AND R(b)) => H(w)");
            var entailsHb = RunWithin(Bound, "Entails(H(b))",
                () => ForwardChaining.Entails(kb, S("H(b)")));
            Assert.That(entailsHb, Is.False,
                "H(b) is not entailed: premise 1 binds w to a, so only H(a) follows; " +
                "deriving H(b) means the fact variable binding x->a was overwritten by x->b");
        }

        [Test]
        public void Finding01_ForwardChaining_FactVariables_EntailedFactIsNotLost() {
            var kb = Set("P(x,x)", "R(x)", "(P(w,a) AND R(b)) => H(w)");
            var entailsHa = RunWithin(Bound, "Entails(H(a))",
                () => ForwardChaining.Entails(kb, S("H(a)")));
            Assert.That(entailsHa, Is.True,
                "P(a,a) and R(b) hold (both facts are universally quantified), so H(a) is entailed");
        }

        // Finding 1 (aliasing variant) — two premises matching the same unrenamed fact share
        // its variable, so the closure only contains the diagonal Q(x,x).
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoCrossPremiseAliasing() {
            var kb = Set("P(x)", "(P(z) AND P(w)) => Q(z,w)");
            var entails = RunWithin(Bound, "Entails(Q(a,b))",
                () => ForwardChaining.Entails(kb, S("Q(a,b)")));
            Assert.That(entails, Is.True,
                "P is universal, so Q(a,b) follows for any pair; only deriving Q(x,x) means " +
                "both premises aliased the same fact variable");
        }

        // Finding 1 (cyclic-substitution variant) — with unrenamed facts this KB built the
        // cyclic substitution {w->x, x->y, y->x} and killed the process with a stack overflow
        // in Substitution.Walk (uncatchable, hence no test existed before the fix).
        [Test]
        public void Finding01_ForwardChaining_FactVariables_NoCyclicSubstitution() {
            var kb = Set("P(x)", "R(y)", "S(x)", "(P(w) AND R(w) AND S(w)) => T(w)");
            var entails = RunWithin(Bound, "Entails(T(a))",
                () => ForwardChaining.Entails(kb, S("T(a)")));
            Assert.That(entails, Is.True, "P, R, and S are universal, so T(a) follows");
        }

        // Finding 3 — Unificator.UnifyLiteral compares Symbol, Arity, and Terms but not Time
        // on predicates (the proposition branch does check Time), so temporally distinct
        // literals unify and a fact at time 1 proves the same fact at any other time.
        [Test]
        public void Finding03_Unification_TimeIndexedPredicates_DoNotUnifyAcrossTimes() {
            var u = new Unificator(S("P(a)^1"), S("P(a)^2"));
            Assert.That(u.IsUnifiable, Is.False,
                "P(a)^1 and P(a)^2 are distinct atoms (AtomicSentence.Equals distinguishes Time)");
        }

        [Test]
        public void Finding03_Entailment_TimeIndexedFact_DoesNotProveOtherTimes() {
            var theory = new Theory(Set("P(a)^1"));
            var entails = RunWithin(Bound, "Entails(P(a)^2)", () => theory.Entails(S("P(a)^2")));
            Assert.That(entails, Is.False,
                "a fact holding at time 1 must not entail the same fact at time 2");
        }

        // Finding 4 — IsUnsatisfiable skips SimplifyConstants when the input is already CNF,
        // so TRUE/FALSE are treated as ordinary resolvable atoms.
        [Test]
        public void Finding04_Resolution_FalseConstant_IsUnsatisfiable() {
            var refuted = RunWithin(Bound, "IsUnsatisfiable(FALSE)",
                () => Resolution.IsUnsatisfiable(S("FALSE"), false, 200));
            Assert.That(refuted, Is.True, "the constant FALSE has no model");
        }

        [Test]
        public void Finding04_Resolution_DerivedFalseClause_IsRecognized() {
            var refuted = RunWithin(Bound, "IsUnsatisfiable((P OR FALSE) AND NOT P)",
                () => Resolution.IsUnsatisfiable(S("(P OR FALSE) AND (NOT P)"), false, 200));
            Assert.That(refuted, Is.True,
                "resolving P out leaves the clause {FALSE}, which is the empty clause in disguise");
        }

        [Test]
        public void Finding04_Resolution_TrueConstant_IsAlwaysEntailed() {
            var entailed = RunWithin(Bound, "Resolve(P, TRUE)",
                () => Resolution.Resolve(S("P"), S("TRUE"), false, 200));
            Assert.That(entailed, Is.True, "every knowledge base entails TRUE");
        }

        // Finding 6 — SkolemForm walks the prenex prefix into a Dictionary keyed by variable,
        // so a prefix that rebinds an existential name (legal, and preserved by ToPrenexForm,
        // which only collapses adjacent identical quantifiers) throws ArgumentException.
        [Test]
        public void Finding06_SkolemForm_RepeatedExistentialVariable_DoesNotThrow() {
            var sentence = S("EXISTS x (FORALL y (EXISTS x (P(x))))");
            Assert.That(() => Logic.SkolemForm(sentence), Throws.Nothing);
        }

        // Finding 7 — Skolem witnesses are named sk1, sk2, ... which are parseable identifiers,
        // so a user constant named sk1 can resolve against a fresh witness.
        [Test]
        public void Finding07_SkolemNames_DoNotCollideWithUserSymbols() {
            FirstOrderLogicExtensions.ResetSkolemCounter();
            var theory = new Theory(Set("P(sk1)"));
            var entails = RunWithin(Bound, "Entails(FORALL x P(x))",
                () => theory.Entails(S("FORALL x (P(x))")));
            Assert.That(entails, Is.False,
                "P(sk1) for one constant does not entail FORALL x P(x); a true result means " +
                "the Skolem witness for the negated goal collided with the user constant sk1");
        }

        // Finding 8 — PushNegation guards only on IsBinary and FlipBinary maps every
        // non-conjunction (including =>) to AND, so NOT (A => B) becomes (NOT A) AND (NOT B)
        // instead of A AND (NOT B). Leaving the sentence untouched would also be acceptable.
        [Test]
        public void Finding08_PushNegation_OverImplication_IsEquivalencePreserving() {
            var sentence = S("NOT (A => B)");
            TransformationFOL.Transform(TransformationFOL.EquivType.PushNegation, ref sentence);
            Assert.That(sentence,
                Is.EqualTo(S("A AND (NOT B)")).Or.EqualTo(S("NOT (A => B)")),
                $"NOT (A => B) is equivalent to A AND (NOT B); got: {sentence}");
        }

        // Finding 8 — PullQuantifier keeps the quantifier symbol when pulling out of an
        // implication antecedent, but (FORALL x P(x)) => Q is EXISTS x (P(x) => Q).
        [Test]
        public void Finding08_PullQuantifier_FromAntecedent_FlipsTheQuantifier() {
            var sentence = S("(FORALL x (P(x))) => Q(a)");
            TransformationFOL.Transform(TransformationFOL.EquivType.PullQuantifier, ref sentence);
            Assert.That(sentence,
                Is.EqualTo(S("EXISTS x (P(x) => Q(a))")).Or.EqualTo(S("(FORALL x (P(x))) => Q(a)")),
                $"pulling FORALL out of an antecedent must flip it to EXISTS; got: {sentence}");
        }

        // Finding 11 — when the positive proof of a NAF target exceeds the remaining depth
        // budget, the empty sub-proof enumeration is read as "not derivable" and NAF spuriously
        // succeeds: a wrong "yes" instead of the documented conservative "no".
        [Test]
        public void Finding11_BackwardChaining_DepthCutoff_MustNotInvertNaf() {
            var kb = Set(
                "Thing(a)", "Base(a)",
                "Base(x) => D1(x)", "D1(x) => D2(x)", "D2(x) => D3(x)", "D3(x) => P(x)",
                "(Thing(x) AND NAF P(x)) => Q(x)");
            var goal = S("Q(a)");

            Assert.That(new BackwardChaining().Entails(kb, goal), Is.False,
                "sanity: P(a) is derivable, so NAF P(a) fails and Q(a) is not entailed");
            Assert.That(new BackwardChaining(3).Entails(kb, goal), Is.False,
                "a depth-limited engine may miss an entailment but must not turn the cutoff " +
                "into a spurious NAF success");
        }

        // Finding 12 — Unificator.GetHashCode iterates dictionary insertion order and Equals
        // short-circuits on the hash, so content-equal unifiers built in different orders
        // compare unequal and are not deduplicated in hash sets.
        [Test]
        public void Finding12_Unificator_ContentEqualBindings_AreEqualRegardlessOfOrder() {
            var u1 = new Unificator(new Dictionary<Variable, Term> {
                { new Variable("x"), new Constant("a") },
                { new Variable("y"), new Constant("b") },
            });
            var u2 = new Unificator(new Dictionary<Variable, Term> {
                { new Variable("y"), new Constant("b") },
                { new Variable("x"), new Constant("a") },
            });

            Assert.That(u1, Is.EqualTo(u2), "same substitution content must compare equal");
            Assert.That(new HashSet<Unificator> { u1, u2 }, Has.Count.EqualTo(1),
                "content-equal unifiers must collapse to one hash-set entry");
        }
    }
}
