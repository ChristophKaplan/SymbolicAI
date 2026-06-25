using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests
{
    public class TheoryTests : TestBase
    {
        // ── Entailment (resolution-backed) ────────────────────────────────────────

        [Test]
        public void Entails_ModusPonens() =>
            Assert.That(new Theory(Set("A", "A => B")).Entails(S("B")), Is.True);

        [Test]
        public void Entails_PropositionalTransitivity() =>
            Assert.That(new Theory(Set("A => B", "B => C", "A")).Entails(S("C")), Is.True);

        [Test]
        public void Entails_DisjunctiveSyllogism() =>
            Assert.That(new Theory(Set("A OR B", "NOT A")).Entails(S("B")), Is.True);

        [Test]
        public void Entails_NotEntailed_False() =>
            Assert.That(new Theory(Set("A")).Entails(S("B")), Is.False);

        [Test]
        public void Entails_FirstOrderModusPonens() =>
            Assert.That(new Theory(Set("Human(Sokrates)", "Human(x) => Mortal(x)")).Entails(S("Mortal(Sokrates)")), Is.True);

        [Test]
        public void Entails_FirstOrderTransitivity() =>
            Assert.That(new Theory(Set("P(x) => Q(x)", "Q(x) => R(x)", "P(a)")).Entails(S("R(a)")), Is.True);

        // ── Equality ────────────────────────────────────────────────────────────

        [Test]
        public void Equals_True_ForSameSentenceSequence()
        {
            var a = new Theory(Set("Have(Alice, Money)", "Owns(Alice, Housea)"));
            var b = new Theory(Set("Have(Alice, Money)", "Owns(Alice, Housea)"));
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_False_ForDifferentSentences()
        {
            var a = new Theory(Set("Have(Alice, Money)"));
            var b = new Theory(Set("Have(Bob, Money)"));
            Assert.That(a.Equals(b), Is.False);
        }

        // ── Agreements / Conflicts against another theory ───────────────────────

        [Test]
        public void Agreements_WhenSentencePresent()
        {
            var a = new Theory(Set("Have(Alice, Money)"));
            var b = new Theory(Set("Have(Alice, Money)"));
            Assert.That(a.Agreements(b).Count, Is.EqualTo(1));
            Assert.That(a.Conflicts(b), Is.Empty);
        }

        [Test]
        public void Conflicts_WhenNegationPresent()
        {
            var claim = S("Have(Alice, Money)");
            var conflicts = new Theory(new List<ISentence> { claim.Negated() })
                .Conflicts(new Theory(new List<ISentence> { claim }));
            Assert.That(conflicts.Count, Is.EqualTo(1));
        }

        [Test]
        public void NeitherAgreementNorConflict_WhenIndependent()
        {
            var a = new Theory(Set("Have(Bob, Money)"));
            var b = new Theory(Set("Have(Alice, Money)"));
            Assert.That(a.Agreements(b), Is.Empty);
            Assert.That(a.Conflicts(b), Is.Empty);
        }

        [Test]
        public void Empty_OnEmptyOrNullInputs()
        {
            Assert.That(new Theory(new List<ISentence>())
                .Agreements(new Theory(Set("Have(Alice, Money)"))), Is.Empty);
            Assert.That(new Theory(Set("Have(Alice, Money)")).Agreements(null), Is.Empty);
            Assert.That(new Theory(Set("Have(Alice, Money)")).Conflicts(null), Is.Empty);
        }

        [Test]
        public void SplitAgreementAndConflict()
        {
            var shared = S("Owns(Alice, Housea)");
            var disputed = S("Have(Alice, Money)");
            var a = new Theory(new List<ISentence> { shared, disputed });
            var b = new Theory(new List<ISentence> { shared, disputed.Negated() });
            Assert.That(a.Agreements(b).Count, Is.EqualTo(1));
            Assert.That(a.Conflicts(b).Count, Is.EqualTo(1));
        }

        // ── Chaining vs semantic ────────────────────────────────────────────────

        [Test]
        public void Chaining_NoConflict_WhenNoLiteralClash()
        {
            var a = new Theory(Set("Have(Alice, Money)"));
            var b = new Theory(Set("Owns(Alice, Housea)"));
            Assert.That(a.Conflicts(b, ComparisonMode.Syntactic), Is.Empty);
        }

        [Test]
        public void Chaining_FindsDirectNegation()
        {
            var claim = S("Have(Alice, Money)");
            var conflicts = new Theory(new List<ISentence> { claim })
                .Conflicts(new Theory(new List<ISentence> { claim.Negated() }), ComparisonMode.Syntactic);
            Assert.That(conflicts.Count, Is.EqualTo(1));
            Assert.That(conflicts[0], Is.EqualTo(claim));
        }

        [Test]
        public void Semantic_FindsDirectNegation()
        {
            var claim = S("Have(Alice, Money)");
            var conflicts = new Theory(new List<ISentence> { claim })
                .Conflicts(new Theory(new List<ISentence> { claim.Negated() }), ComparisonMode.Semantic);
            Assert.That(conflicts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Semantic_CatchesChainedContradiction()
        {
            var conflicts = new Theory(Set("Rich(Alice)"))
                .Conflicts(new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)")), ComparisonMode.Semantic);
            Assert.That(conflicts, Is.Not.Empty);
        }

        [Test]
        public void Semantic_IsConsistentWith_CatchesJointInconsistency()
        {
            // Each side alone is consistent, and no single sentence of one is refuted by the other
            // (the clash needs premises from both sides), so only the union check can see it.
            var a = new Theory(Set("P", "P => Q"));
            var b = new Theory(Set("Q => R", "NOT R"));
            Assert.That(a.Conflicts(b, ComparisonMode.Semantic), Is.Empty);
            Assert.That(b.Conflicts(a, ComparisonMode.Semantic), Is.Empty);
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Semantic), Is.False);
            Assert.That(b.IsConsistentWith(a, ComparisonMode.Semantic), Is.False);
        }

        [Test]
        public void Semantic_IsConsistentWith_True_WhenUnionSatisfiable()
        {
            var a = new Theory(Set("P", "P => Q"));
            var b = new Theory(Set("Q => R"));
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Semantic), Is.True);
        }

        [Test]
        public void IsConsistentWith_IsSymmetric_ForChainedContradiction()
        {
            // The contradiction only surfaces when scanning A's `Rich(Alice)` against B's rules,
            // so the directional Conflicts from B misses it but IsConsistentWith must not.
            var a = new Theory(Set("Rich(Alice)"));
            var b = new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)"));
            Assert.That(b.Conflicts(a, ComparisonMode.Semantic), Is.Empty);
            Assert.That(b.IsConsistentWith(a, ComparisonMode.Semantic), Is.False);
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Semantic), Is.False);
        }

        // ── Chaining mode derivations ───────────────────────────────────────────

        [Test]
        public void Chaining_FindsDerivedAgreement()
        {
            // Syntactically silent (Mortal never literally stated), but the closure derives it.
            var agreements = new Theory(Set("Mortal(Sokrates)"))
                .Agreements(new Theory(Set("Human(Sokrates)", "Human(x) => Mortal(x)")), ComparisonMode.Syntactic);
            Assert.That(agreements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Chaining_FindsDerivedContradiction()
        {
            var conflicts = new Theory(Set("Rich(Alice)"))
                .Conflicts(new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)")), ComparisonMode.Syntactic);
            Assert.That(conflicts.Count, Is.EqualTo(1));
            Assert.That(conflicts[0].Negated().ToString(), Is.EqualTo(S("-Rich(Alice)").ToString()));
        }

        [Test]
        public void Chaining_NonLiterals_AgreeByIdentity()
        {
            // A shared rule counts as agreement; chaining adds nothing for non-literals.
            var agreements = new Theory(Set("Human(x) => Mortal(x)"))
                .Agreements(new Theory(Set("Human(x) => Mortal(x)")), ComparisonMode.Syntactic);
            Assert.That(agreements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Chaining_NonLiterals_ConflictByIdentity()
        {
            // The complement of a stated rule counts as a conflict (non-literals compare by identity).
            var rule = S("Human(x) => Mortal(x)");
            var conflicts = new Theory(new List<ISentence> { rule })
                .Conflicts(new Theory(new List<ISentence> { rule.Negated() }), ComparisonMode.Syntactic);
            Assert.That(conflicts.Count, Is.EqualTo(1));
        }

        [Test]
        public void Chaining_IsConsistentWith_CatchesJointInconsistency()
        {
            // Each side alone is consistent and neither directional Compare sees the clash:
            // only the union's closure derives ¬Q(a) against B's Q(a).
            var a = new Theory(Set("P(a)", "P(x) => -Q(x)"));
            var b = new Theory(Set("Q(a)"));
            Assert.That(a.IsConsistent(), Is.True);
            Assert.That(b.IsConsistent(), Is.True);
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Syntactic), Is.False);
        }

        // ── Internal consistency (closure conflicts) ────────────────────────────

        [Test]
        public void Conflicts_FindsRuleDrivenSelfConflict()
        {
            // A fact plus a rule whose consequent contradicts another fact.
            var merged = new Theory(Set("Penguin(p)", "Flies(p)", "Penguin(z) => -Flies(z)"));
            var conflicts = merged.Inconsistencies();
            Assert.That(conflicts.Count, Is.EqualTo(1));
            Assert.That(conflicts[0].ToString(), Is.EqualTo(S("Flies(p)").ToString()));
            Assert.That(merged.IsConsistent(), Is.False);
        }

        [Test]
        public void Conflicts_Empty_WhenClosureIsClean()
        {
            var theory = new Theory(Set("Penguin(p)", "Penguin(z) => Swims(z)"));
            Assert.That(theory.Inconsistencies(), Is.Empty);
            Assert.That(theory.IsConsistent(), Is.True);
        }

        // ── Complement safety ───────────────────────────────────────────────────

        [Test]
        public void Conflicts_DoesNotMutateParentedSentences()
        {
            // A sentence extracted from an implication still carries its parent linkage;
            // Conflicts must neither throw nor splice a negation into the source tree.
            var rule = S("A => NOT B");
            var consequent = rule.Children[1];
            var before = rule.ToString();

            var conflicts = new Theory(new List<ISentence> { consequent })
                .Conflicts(new Theory(Set("B")), ComparisonMode.Semantic);

            Assert.That(conflicts.Count, Is.EqualTo(1));
            Assert.That(rule.ToString(), Is.EqualTo(before));
        }

        // ── Explain (kernels) ───────────────────────────────────────────────────

        [Test]
        public void Explain_ReturnsKernelWhenEveryPremiseIsLoadBearing()
        {
            // The only proof of ¬Rich needs both premises, so the kernel equals the whole theory.
            var theory = new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)"));
            var kernels = theory.Explain(S("Rich(Alice)").Negated());
            Assert.That(kernels.Count, Is.EqualTo(1));
            Assert.That(kernels[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Explain_Empty_WhenNotEntailed()
        {
            var theory = new Theory(Set("Poor(Alice)"));
            Assert.That(theory.Explain(S("Rich(Alice)").Negated()), Is.Empty);
        }
    }
}
