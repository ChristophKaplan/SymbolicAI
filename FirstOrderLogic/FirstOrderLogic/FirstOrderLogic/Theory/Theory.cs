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
        
        public static List<(ISentence Claim, ISentence Counter)> FindAllConflicts(IReadOnlyList<ISentence> literals)
        {
            var pairs = new List<(ISentence Claim, ISentence Counter)>();
            for (var i = 0; i < literals.Count; i++)
            {
                for (var j = i + 1; j < literals.Count; j++)
                {
                    if (!literals[i].IsNegationOf(literals[j])) continue;

                    (ISentence Claim, ISentence Counter) tuple = literals[i].IsNegation
                        ? new (literals[j], literals[i])
                        : new (literals[i], literals[j]);
                    pairs.Add(tuple);
                }
            }

            return pairs;
        }
        
        public bool Entails(ISentence target)
        {
            if (State.Count == 0) return false;
            return Resolution.Resolve(Conjoin(State), target);
        }

        public List<List<ISentence>> Explain(ISentence target) => _kernels.FindAllKernels(State, target);
        
        public TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining)
        {
            var agreements     = new List<ISentence>();
            var contradictions = new List<(ISentence Claim, ISentence Counter)>();
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
                    contradictions.Add(new (s, counter));
                else
                    silences.Add(s);
            }

            return new TheoryComparison(agreements, contradictions, silences, mode);
        }
        
        public bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Chaining)
        {
            var union = new List<ISentence>(State);
            if (other?.State != null) union.AddRange(other.State);

            switch (mode)
            {
                case ComparisonMode.Chaining:
                    return FindAllConflicts(ForwardChaining.Saturate(union)).Count == 0;
                case ComparisonMode.Semantic:
                    return union.Count == 0 || !Resolution.IsUnsatisfiable(_logic.ToConjunctiveNormalForm(Conjoin(union)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode");
            }
        }
        
        public List<(ISentence Claim, ISentence Counter)> Conflicts() => FindAllConflicts(ForwardChaining.Saturate(State));

        public bool IsConsistent() => Conflicts().Count == 0;

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
            _logic.ConnectSentences(sentences.ToList());

        private static ISentence Complement(ISentence s) => s.Negated();

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
