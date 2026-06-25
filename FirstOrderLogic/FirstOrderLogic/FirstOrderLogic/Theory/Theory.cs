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
        
        public List<(ISentence Claim, ISentence Counter)> Conflicts() => ForwardChaining.Saturate(State).Conflicts();

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

            var heldByOther = HeldByOther(other, mode);

            foreach (var s in State)
            {
                if (s == null) continue;
                var counter = s.Negated();

                if (heldByOther(s)) agreements.Add(s);
                else if (heldByOther(counter)) contradictions.Add(new(s, counter));
                else silences.Add(s);
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
                    return ForwardChaining.Saturate(union).Conflicts().Count == 0;
                case ComparisonMode.Semantic:
                    return union.Count == 0 || !Resolution.IsUnsatisfiable(_logic.ToConjunctiveNormalForm(Conjoin(union)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode");
            }
        }
        
        public bool IsConsistent() => Conflicts().Count == 0;
        
        private static Func<ISentence, bool> HeldByOther(ITheory? other, ComparisonMode mode)
        {
            if (other?.State == null) return _ => false;

            switch (mode)
            {
                case ComparisonMode.Semantic:
                    return other.Entails;
                case ComparisonMode.Chaining:
                    var closure = ForwardChaining.Saturate(other.State);
                    // Chaining derives only literals; a non-literal (rule) can only be matched syntactically.
                    return s => s.IsLiteral
                        ? ForwardChaining.Holds(closure, s)
                        : other.State.Any(x => x != null && x.Equals(s));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static ISentence Conjoin(IReadOnlyList<ISentence> sentences) => _logic.ConnectSentences(sentences.ToList());
        

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
