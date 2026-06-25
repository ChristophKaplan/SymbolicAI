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

        [Test]
        public void Equals_False_AgainstNullAndOtherType()
        {
            var a = new Theory(Set("Have(Alice, Money)"));
            Assert.That(a.Equals(null), Is.False);
            Assert.That(a!.Equals("not a theory"), Is.False);
        }

        // ── Compare: alignment buckets ──────────────────────────────────────────

        [Test]
        public void Compare_Agreement_WhenSentencePresent()
        {
            var cmp = new Theory(Set("Have(Alice, Money)"))
                .Compare(new Theory(Set("Have(Alice, Money)")));
            Assert.That(cmp.Agreements.Count, Is.EqualTo(1));
            Assert.That(cmp.Contradictions, Is.Empty);
            Assert.That(cmp.Alignment, Is.EqualTo(1f));
        }

        [Test]
        public void Compare_Contradiction_WhenNegationPresent()
        {
            var claim = S("Have(Alice, Money)");
            var cmp = new Theory(new List<ISentence> { claim.Negated() })
                .Compare(new Theory(new List<ISentence> { claim }));
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
            Assert.That(cmp.HasContradiction, Is.True);
            Assert.That(cmp.IsConsistent, Is.False);
            Assert.That(cmp.Alignment, Is.EqualTo(0f));
        }

        [Test]
        public void Compare_Silence_WhenIndependent()
        {
            var cmp = new Theory(Set("Have(Bob, Money)"))
                .Compare(new Theory(Set("Have(Alice, Money)")));
            Assert.That(cmp.Silences.Count, Is.EqualTo(1));
            Assert.That(cmp.Agreements, Is.Empty);
            Assert.That(cmp.Contradictions, Is.Empty);
            Assert.That(cmp.Alignment, Is.EqualTo(0.5f));
        }

        [Test]
        public void Compare_NeutralAlignment_OnEmptyOrNullInputs()
        {
            Assert.That(new Theory(new List<ISentence>())
                .Compare(new Theory(Set("Have(Alice, Money)"))).Alignment, Is.EqualTo(0.5f));
            Assert.That(new Theory(Set("Have(Alice, Money)"))
                .Compare(null).Alignment, Is.EqualTo(0.5f));
        }

        [Test]
        public void Compare_SplitAgreementContradiction_IsHalf()
        {
            var shared = S("Owns(Alice, Housea)");
            var disputed = S("Have(Alice, Money)");
            var cmp = new Theory(new List<ISentence> { shared, disputed })
                .Compare(new Theory(new List<ISentence> { shared, disputed.Negated() }));
            Assert.That(cmp.Agreements.Count, Is.EqualTo(1));
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
            Assert.That(cmp.Alignment, Is.EqualTo(0.5f));
        }

        [Test]
        public void Compare_PrefersAgreementOverContradiction()
        {
            var p = S("Have(Alice, Money)");
            var cmp = new Theory(new List<ISentence> { p })
                .Compare(new Theory(new List<ISentence> { p.Negated(), p }));
            Assert.That(cmp.Agreements.Count, Is.EqualTo(1));
            Assert.That(cmp.Contradictions, Is.Empty);
        }

        // ── Compare: consistency (chaining vs semantic) ─────────────────────────

        [Test]
        public void Chaining_Consistent_WhenNoLiteralClash()
        {
            var cmp = new Theory(Set("Have(Alice, Money)"))
                .Compare(new Theory(Set("Owns(Alice, Housea)")), ComparisonMode.Chaining);
            Assert.That(cmp.IsConsistent, Is.True);
            Assert.That(cmp.Contradictions, Is.Empty);
        }

        [Test]
        public void Chaining_FindsDirectNegation()
        {
            var claim = S("Have(Alice, Money)");
            var cmp = new Theory(new List<ISentence> { claim })
                .Compare(new Theory(new List<ISentence> { claim.Negated() }), ComparisonMode.Chaining);
            Assert.That(cmp.IsConsistent, Is.False);
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
            Assert.That(cmp.Contradictions[0].Claim, Is.EqualTo(claim));
        }

        [Test]
        public void Semantic_FindsDirectNegation()
        {
            var claim = S("Have(Alice, Money)");
            var cmp = new Theory(new List<ISentence> { claim })
                .Compare(new Theory(new List<ISentence> { claim.Negated() }), ComparisonMode.Semantic);
            Assert.That(cmp.IsConsistent, Is.False);
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
        }

        [Test]
        public void Semantic_CatchesChainedContradiction()
        {
            var cmp = new Theory(Set("Rich(Alice)"))
                .Compare(new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)")), ComparisonMode.Semantic);
            Assert.That(cmp.IsConsistent, Is.False);
        }

        [Test]
        public void Semantic_IsConsistentWith_CatchesJointInconsistency()
        {
            // Each side alone is consistent, and no single sentence of one is refuted by the other
            // (the clash needs premises from both sides), so only the union check can see it.
            var a = new Theory(Set("P", "P => Q"));
            var b = new Theory(Set("Q => R", "NOT R"));
            Assert.That(a.Compare(b, ComparisonMode.Semantic).IsConsistent, Is.True);
            Assert.That(b.Compare(a, ComparisonMode.Semantic).IsConsistent, Is.True);
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
            // so the directional Compare from B misses it but IsConsistentWith must not.
            var a = new Theory(Set("Rich(Alice)"));
            var b = new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)"));
            Assert.That(b.Compare(a, ComparisonMode.Semantic).IsConsistent, Is.True);
            Assert.That(b.IsConsistentWith(a, ComparisonMode.Semantic), Is.False);
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Semantic), Is.False);
        }

        // ── Compare: chaining mode ──────────────────────────────────────────────

        [Test]
        public void Chaining_FindsDerivedAgreement()
        {
            // Syntactically silent (Mortal never literally stated), but the closure derives it.
            var cmp = new Theory(Set("Mortal(Sokrates)"))
                .Compare(new Theory(Set("Human(Sokrates)", "Human(x) => Mortal(x)")), ComparisonMode.Chaining);
            Assert.That(cmp.Agreements.Count, Is.EqualTo(1));
            Assert.That(cmp.Alignment, Is.EqualTo(1f));
        }

        [Test]
        public void Chaining_FindsDerivedContradiction()
        {
            var cmp = new Theory(Set("Rich(Alice)"))
                .Compare(new Theory(Set("Poor(Alice)", "Poor(Alice) => -Rich(Alice)")), ComparisonMode.Chaining);
            Assert.That(cmp.IsConsistent, Is.False);
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
            Assert.That(cmp.Contradictions[0].Counter.ToString(), Is.EqualTo(S("-Rich(Alice)").ToString()));
        }

        [Test]
        public void Chaining_NonLiterals_CompareByIdentity()
        {
            // A shared rule counts as agreement; chaining adds nothing for non-literals.
            var cmp = new Theory(Set("Human(x) => Mortal(x)"))
                .Compare(new Theory(Set("Human(x) => Mortal(x)")), ComparisonMode.Chaining);
            Assert.That(cmp.Agreements.Count, Is.EqualTo(1));
        }

        [Test]
        public void Chaining_NonLiterals_ContradictByIdentity()
        {
            // The complement of a stated rule counts as a contradiction (non-literals compare by identity).
            var rule = S("Human(x) => Mortal(x)");
            var cmp = new Theory(new List<ISentence> { rule })
                .Compare(new Theory(new List<ISentence> { rule.Clone().Negated() }), ComparisonMode.Chaining);
            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
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
            Assert.That(a.IsConsistentWith(b, ComparisonMode.Chaining), Is.False);
        }

        // ── Internal consistency (closure conflicts) ────────────────────────────

        [Test]
        public void Conflicts_FindsRuleDrivenSelfConflict()
        {
            // The is/ought pattern: a fact plus a norm whose consequent contradicts another fact.
            var merged = new Theory(Set("IsFemale(m)", "Work(m)", "IsFemale(z) => -Work(z)"));
            var conflicts = merged.Conflicts();
            Assert.That(conflicts.Count, Is.EqualTo(1));
            Assert.That(conflicts[0].Claim.ToString(), Is.EqualTo(S("Work(m)").ToString()));
            Assert.That(merged.IsConsistent(), Is.False);
        }

        [Test]
        public void Conflicts_Empty_WhenClosureIsClean()
        {
            var theory = new Theory(Set("IsFemale(m)", "IsFemale(z) => CanCook(z)"));
            Assert.That(theory.Conflicts(), Is.Empty);
            Assert.That(theory.IsConsistent(), Is.True);
        }

        // ── Complement safety ───────────────────────────────────────────────────

        [Test]
        public void Contradicts_DoesNotMutateParentedSentences()
        {
            // A sentence extracted from an implication still carries its parent linkage;
            // Compare must neither throw nor splice a negation into the source tree.
            var rule = S("A => NOT B");
            var consequent = rule.Children[1];
            var before = rule.ToString();

            var cmp = new Theory(new List<ISentence> { consequent })
                .Compare(new Theory(Set("B")), ComparisonMode.Semantic);

            Assert.That(cmp.Contradictions.Count, Is.EqualTo(1));
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
