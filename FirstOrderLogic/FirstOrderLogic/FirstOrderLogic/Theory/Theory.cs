using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
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

        // Resolution-backed (sound + refutation-complete, but exponential) — on-demand only.
        public bool Entails(ISentence target)
        {
            if (State.Count == 0) return false;
            var cnf = _logic.ToConjunctiveNormalForm(Conjoin(State));
            return Resolution.Resolve(cnf, target.Clone());
        }

        public List<List<ISentence>> Explain(ISentence target) => _kernels.FindAllKernels(State, target);

        // Directional: classify each of this theory's sentences against `other` as agreement,
        // contradiction (its complement holds), or silence.
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

        // Symmetric joint consistency of the union: Chaining checks the union's closure for
        // complementary literals, Semantic checks satisfiability, Syntactic (no inference)
        // checks both Compare directions for literal clashes.
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
                        || !Resolution.IsUnsatisfiable(_logic.ToConjunctiveNormalForm(Conjoin(union)));
                default:
                    return !Compare(other, mode).HasContradiction
                        && (other == null || !other.Compare(this, mode).HasContradiction);
            }
        }

        // The complementary literal pairs in this theory's own deductive closure.
        public List<TheoryConflict> Conflicts() =>
            TheoryConflict.FindAll(ForwardChaining.Saturate(State));

        public bool IsConsistent() => Conflicts().Count == 0;

        // Semantic asks Resolution, Chaining looks literals up in the precomputed closure,
        // everything else falls back to sentence identity.
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
            _logic.ConnectSentences(sentences.Select(s => s.Clone()).ToList());

        // Mutation-free complement: ISentence.Negate() splices into the sentence's parent tree,
        // so it must never be called on sentences the theory does not own.
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
