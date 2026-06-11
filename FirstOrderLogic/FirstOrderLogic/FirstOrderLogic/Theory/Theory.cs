using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    [Serializable]
    public class Theory : ITheory
    {
        private static readonly FirstOrderLogic _logic = new();
        private static readonly Resolution _resolution = new();
        private static readonly KernelSets _kernels = new();

        public List<ISentence> State { get; private set; }

        public Theory(List<ISentence> state)
        {
            State = state;
        }

        // Does this theory prove `target`? Resolution-backed (sound + complete, but exponential and
        // non-terminating on non-ground input) — on-demand only, never per-tick.
        public bool Entails(ISentence target)
        {
            if (State == null || State.Count == 0) return false;

            var conjunction = State[0].Clone();
            for (var i = 1; i < State.Count; i++)
                conjunction = new ComplexSentence(
                    conjunction, Connective.LogicSymbol.CONJUNCTION, State[i].Clone());

            // Internal failures (e.g. an unsupported sentence shape reaching CNF conversion) are
            // surfaced rather than silently swallowed and reported as "not entailed".
            var cnf = _logic.ToConjunctiveNormalForm(conjunction);
            return _resolution.Resolve(cnf, target.Clone());
        }

        public List<List<ISentence>> Explain(ISentence target) =>
            State == null ? new List<List<ISentence>>() : _kernels.FindAllKernels(State, target);

        // Directional: classify each of this theory's sentences against `other`.
        // Syntactic compares literals; Semantic asks `other` to entail them (catches chained inference,
        // ground sentences only — expensive, on-demand).
        public TheoryComparison Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            var agreements     = new List<ISentence>();
            var contradictions = new List<TheoryConflict>();
            var silences       = new List<ISentence>();

            if (State != null)
            {
                foreach (var s in State)
                {
                    if (s == null) continue;
                    if (Agrees(other, s, mode))
                        agreements.Add(s);
                    else if (Contradicts(other, s, mode, out var counter))
                        contradictions.Add(new TheoryConflict(s, counter));
                    else
                        silences.Add(s);
                }
            }

            return new TheoryComparison(agreements, contradictions, silences, mode);
        }

        public bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            if (Compare(other, mode).HasContradiction) return false;
            if (other != null && other.Compare(this, mode).HasContradiction) return false;
            return true;
        }

        private static bool Agrees(ITheory? other, ISentence s, ComparisonMode mode)
        {
            if (other?.State == null) return false;
            if (mode == ComparisonMode.Semantic) return other.Entails(s);
            foreach (var x in other.State)
                if (x != null && x.Equals(s)) return true;
            return false;
        }

        private static bool Contradicts(ITheory? other, ISentence s, ComparisonMode mode, out ISentence counter)
        {
            counter = s.Negate();
            if (other?.State == null) return false;
            if (mode == ComparisonMode.Semantic) return other.Entails(counter);
            foreach (var x in other.State)
                if (x != null && s.IsNegationOf(x)) { counter = x; return true; }
            return false;
        }

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
