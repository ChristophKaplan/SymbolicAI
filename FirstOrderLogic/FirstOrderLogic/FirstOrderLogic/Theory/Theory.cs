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

        private readonly List<ISentence> _state;

        public IReadOnlyList<ISentence> State => _state;

        public Theory(List<ISentence> state) =>
            _state = (state ?? Enumerable.Empty<ISentence>()).Where(s => s != null).ToList();

        // Intentional mutation of the belief base; State stays a read-only view.
        public bool Contains(ISentence sentence) => _state.Contains(sentence);
        public void Add(ISentence sentence) { if (sentence != null) _state.Add(sentence); }
        public int RemoveAll(Predicate<ISentence> match) => _state.RemoveAll(match);

        public List<ISentence> Inconsistencies() => Inconsistencies(null);

        // Conflicting literals in the closure of our state merged with the other's.
        public List<ISentence> Inconsistencies(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic) =>
            mode switch
            {
                ComparisonMode.Syntactic => ForwardChaining.Saturate(Union(other)).Conflicts(),
                // Semantic refutation yields only a verdict; a witness set needs unsat-core extraction.
                ComparisonMode.Semantic  => throw new NotImplementedException(
                    "Semantic inconsistency witnesses require unsat-core extraction."),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode"),
            };

        public bool IsConsistent() => Inconsistencies().Count == 0;

        public bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            switch (mode)
            {
                case ComparisonMode.Syntactic:
                    return Inconsistencies(other).Count == 0;
                case ComparisonMode.Semantic:
                    var union = Union(other);
                    return union.Count == 0 || !Resolution.IsUnsatisfiable(_logic.ToConjunctiveNormalForm(Conjoin(union)));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode");
            }
        }

        private List<ISentence> Union(ITheory? other)
        {
            var union = new List<ISentence>(State);
            if (other?.State != null) union.AddRange(other.State);
            return union;
        }
        
        
        public bool Entails(ISentence target)
        {
            if (State.Count == 0) return false;
            return Resolution.Resolve(Conjoin(State), target);
        }

        public List<List<ISentence>> Explain(ISentence target) => _kernels.FindAllKernels(State, target);

        // How our sentences stand against the other theory: each is something it holds (agreement),
        // something whose negation it holds (disagreement), or something it is silent on.
        public Stance Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            if (other?.State == null)
                return new Stance(new List<ISentence>(), new List<ISentence>(), State.ToList());

            var closure = ChainingClosure(other, mode);
            var agree = new List<ISentence>();
            var disagree = new List<ISentence>();
            var silent = new List<ISentence>();
            foreach (var s in State)
            {
                if (HeldByOther(other, s, closure)) agree.Add(s);
                else if (HeldByOther(other, s.Negated(), closure)) disagree.Add(s);
                else silent.Add(s);
            }
            return new Stance(agree, disagree, silent);
        }

        // The other theory's derivable literals under chaining; null means use semantic entailment instead.
        private static IReadOnlyList<ISentence>? ChainingClosure(ITheory other, ComparisonMode mode) =>
            mode == ComparisonMode.Syntactic ? ForwardChaining.Saturate(other.State) : null;

        // Does the other theory hold s? With a chaining closure, check literal derivation
        // (a non-literal rule can only be matched by identity); without one, use semantic entailment.
        private static bool HeldByOther(ITheory other, ISentence s, IReadOnlyList<ISentence>? closure)
        {
            if (closure == null) return other.Entails(s);
            return s.IsLiteral
                ? ForwardChaining.Holds(closure, s)
                : other.State.Any(x => x != null && x.Equals(s));
        }

        private static ISentence Conjoin(IReadOnlyList<ISentence> sentences) => _logic.ConnectSentences(sentences.ToList());

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;
            return State.SequenceEqual(((Theory)obj).State);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var sentence in State) hash = hash * 31 + sentence.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => string.Join("\n", State);
    }
}
