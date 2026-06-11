using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // A belief base: a finite set of sentences queried under three inference regimes —
    // identity (Syntactic), deductive closure of the rule subset (Chaining), and full
    // Resolution entailment (Semantic). See ComparisonMode for the strength/cost trade.
    [Serializable]
    public class Theory : ITheory
    {
        private static readonly FirstOrderLogic _logic = new();
        private static readonly KernelSets _kernels = new();

        public List<ISentence> State { get; private set; }

        public Theory(List<ISentence> state)
        {
            State = state ?? new List<ISentence>();
        }

        // Resolution-backed (sound + refutation-complete, but exponential and non-terminating on
        // non-ground input) — on-demand only, never per-tick. Resolution carries per-run counters,
        // so a fresh instance per call keeps this safe under concurrent callers.
        public bool Entails(ISentence target)
        {
            if (State.Count == 0) return false;
            var cnf = _logic.ToConjunctiveNormalForm(Conjoin(State));
            return new Resolution().Resolve(cnf, target.Clone());
        }

        public List<List<ISentence>> Explain(ISentence target) => _kernels.FindAllKernels(State, target);

        // Directional: classify each of this theory's sentences against `other` as agreement
        // (the sentence holds in `other`), contradiction (its complement does), or silence
        // (neither). In Chaining mode `other`'s closure is computed once and literals are checked
        // against it; non-literal sentences fall back to identity.
        public TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            var agreements     = new List<ISentence>();
            var contradictions = new List<TheoryConflict>();
            var silences       = new List<ISentence>();

            var closure = mode == ComparisonMode.Chaining && other?.State != null
                ? ForwardChaining.Saturate(other.State)
                : null;

            foreach (var s in State)
            {
                if (s == null) continue;
                var counter = Complement(s);
                if (HoldsIn(other, closure, s, mode))
                    agreements.Add(s);
                else if (HoldsIn(other, closure, counter, mode))
                    contradictions.Add(new TheoryConflict(s, counter));
                else
                    silences.Add(s);
            }

            return new TheoryComparison(agreements, contradictions, silences, mode);
        }

        // Symmetric. Where the mode can decide it, this is joint consistency of the union — it
        // catches contradictions that only arise from combining the two theories and includes each
        // side's internal conflicts: Chaining checks the union's closure for complementary literal
        // pairs, Semantic checks the union for satisfiability. Syntactic has no inference, so it
        // just checks both Compare directions for literal clashes.
        public bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            var union = new List<ISentence>(State);
            if (other?.State != null) union.AddRange(other.State);

            switch (mode)
            {
                case ComparisonMode.Chaining:
                    return TheoryConflict.FindAll(ForwardChaining.Saturate(union)).Count == 0;
                case ComparisonMode.Semantic:
                    return union.Count == 0
                        || !new Resolution().IsUnsatisfiable(_logic.ToConjunctiveNormalForm(Conjoin(union)));
                default:
                    return !Compare(other, mode).HasContradiction
                        && (other == null || !other.Compare(this, mode).HasContradiction);
            }
        }

        // The complementary literal pairs in this theory's own deductive closure — the internal
        // tensions its rules produce from its facts.
        public List<TheoryConflict> Conflicts() =>
            TheoryConflict.FindAll(ForwardChaining.Saturate(State));

        public bool IsConsistent() => Conflicts().Count == 0;

        // Does `s` hold in `other` under `mode`? Semantic asks Resolution, Chaining looks up
        // literals in the precomputed closure; everything else falls back to sentence identity.
        private static bool HoldsIn(ITheory? other, List<ISentence>? closure, ISentence s, ComparisonMode mode)
        {
            if (other?.State == null) return false;
            switch (mode)
            {
                case ComparisonMode.Semantic:
                    return other.Entails(s);
                case ComparisonMode.Chaining when s.IsLiteral:
                    return ForwardChaining.Holds(closure!, s);
                default:
                    return other.State.Any(x => x != null && x.Equals(s));
            }
        }

        private static ISentence Conjoin(IReadOnlyList<ISentence> sentences) =>
            sentences.Skip(1).Aggregate(sentences[0].Clone(), (acc, s) =>
                (ISentence)new ComplexSentence(acc, Connective.LogicSymbol.CONJUNCTION, s.Clone()));

        // Mutation-free complement. ISentence.Negate() splices the negation into the sentence's
        // parent tree (or throws when the parent linkage is stale), so it must never be called on
        // sentences the theory does not own.
        private static ISentence Complement(ISentence s) =>
            s.IsNegation
                ? s.Children[0].Clone()
                : new ComplexSentence(Connective.LogicSymbol.NEGATION, s.Clone());

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;

            var other = (Theory)obj;
            return State.SequenceEqual(other.State);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var sentence in State) hash = hash * 31 + (sentence?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override string ToString() => string.Join("\n", State);
    }
}
