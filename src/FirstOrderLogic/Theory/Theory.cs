using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    [Serializable]
    public class Theory : ITheory
    {
        private static readonly KernelSets _kernels = new();

        private readonly List<ISentence> _state;
        
        public int Count => _state.Count;
        public ISentence this[int index] => _state[index];
        public IEnumerator<ISentence> GetEnumerator() => _state.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Theory(List<ISentence> state) =>
            _state = state.ToList();

        public List<ISentence> Inconsistencies() => Inconsistencies(null);
        
        public List<ISentence> Inconsistencies(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic) =>
            mode switch
            {
                ComparisonMode.Syntactic => ForwardChaining.Saturate(RequireChainable(Union(other))).Conflicts(),
                ComparisonMode.Semantic  => throw new NotImplementedException(
                    "Semantic inconsistency witnesses require unsat-core extraction."),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode"),
            };

        // Saturation silently ignores sentences outside the literal/rule fragment, so letting one
        // through would report a plainly inconsistent theory as consistent.
        private static List<ISentence> RequireChainable(List<ISentence> sentences)
        {
            var outside = sentences.FirstOrDefault(s => !Rule.IsChainable(s));
            if (outside != null)
            {
                throw new NotSupportedException(
                    $"Syntactic consistency covers only the literal/rule fragment; '{outside}' is outside it. " +
                    "Use IsConsistentWith(other, ComparisonMode.Semantic) instead.");
            }

            return sentences;
        }

        public bool IsConsistent() => Inconsistencies().Count == 0;

        public bool IsConsistentWith(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            switch (mode)
            {
                case ComparisonMode.Syntactic:
                    return Inconsistencies(other).Count == 0;
                case ComparisonMode.Semantic:
                    // Pass the raw conjunction: IsUnsatisfiable prenexes/skolemizes/CNFs itself,
                    // which explicit ToConjunctiveNormalForm cannot do for quantified sentences.
                    var union = Union(other);
                    return union.Count == 0 || !Resolution.IsUnsatisfiable(Conjoin(union));
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid comparison mode");
            }
        }

        private List<ISentence> Union(ITheory? other)
        {
            var union = new List<ISentence>(this);
            if (other != null) union.AddRange(other);
            return union;
        }
        
        public bool Entails(ISentence target)
        {
            // The empty theory entails exactly the tautologies: ⊨ target iff ¬target is unsatisfiable.
            if (this.Count == 0) return Resolution.IsUnsatisfiable(target.Negated());
            return Resolution.Resolve(Conjoin(this), target);
        }

        public List<List<ISentence>> Explain(ISentence target) => _kernels.FindAllKernels(this, target);
        
        public Stance Compare(ITheory? other, ComparisonMode mode = ComparisonMode.Syntactic)
        {
            if (other == null) throw new ArgumentNullException(nameof(other), "Cannot compare against a null theory.");
            var closure = ChainingClosure(other, mode);
            var agree = new List<ISentence>();
            var disagree = new List<ISentence>();
            var silent = new List<ISentence>();
            foreach (var s in this)
            {
                if (HeldByOther(other, s, closure)) agree.Add(s);
                else if (DeniedByOther(other, s, closure)) disagree.Add(s);
                else silent.Add(s);
            }
            return new Stance(agree, disagree, silent);
        }
        
        private static IReadOnlyList<ISentence>? ChainingClosure(ITheory other, ComparisonMode mode) =>
            mode == ComparisonMode.Syntactic ? ForwardChaining.Saturate(other) : null;
        
        private static bool HeldByOther(ITheory other, ISentence s, IReadOnlyList<ISentence>? closure)
        {
            if (closure == null) return other.Entails(s);
            if (s.IsLiteral) return ForwardChaining.Holds(closure, s);
            return other.Any(x => x.Equals(s)) || HoldsRuleForm(other, s, negatedHead: false);
        }
        
        private static bool DeniedByOther(ITheory other, ISentence s, IReadOnlyList<ISentence>? closure)
        {
            if (HeldByOther(other, s.Negated(), closure)) return true;
            return closure != null && !s.IsLiteral && HoldsRuleForm(other, s, negatedHead: true);
        }

        private static bool HoldsRuleForm(ITheory other, ISentence s, bool negatedHead)
        {
            var rule = Rule.From(s);
            if (rule == null) return false;
            return other.Where(x => !x.IsLiteral)
                .Select(Rule.From)
                .Any(candidate => candidate != null && RulesMatch(rule, candidate, negatedHead));
        }
        
        private static bool RulesMatch(Rule rule, Rule candidate, bool negatedHead)
        {
            if (rule.Premises.Count != candidate.Premises.Count) return false;
            if (rule.NafPremises.Count != candidate.NafPremises.Count) return false;

            candidate = candidate.Renamed(0);
            var left = rule.Premises.Concat(rule.NafPremises).Append(rule.Head).ToList();
            var right = candidate.Premises.Concat(candidate.NafPremises)
                .Append(negatedHead ? candidate.Head.Negated() : candidate.Head).ToList();
            var positiveCount = rule.Premises.Count;

            // Premise order carries no logical meaning: each left premise must match a distinct
            // right premise of the same kind — positive with positive, NAF with NAF (backtracking
            // over a used-set; premise lists are small). The heads — last in both lists — are
            // still matched only against each other.
            return MatchFrom(left, 0, right, new bool[right.Count]);

            bool MatchFrom(List<ISentence> left, int i, List<ISentence> right, bool[] used)
            {
                if (i == left.Count) return true;

                var headIndex = left.Count - 1;
                int first, last;
                if (i == headIndex) {
                    first = headIndex;
                    last = headIndex;
                }
                else if (i < positiveCount) {
                    first = 0;
                    last = positiveCount - 1;
                }
                else {
                    first = positiveCount;
                    last = headIndex - 1;
                }
                for (var j = first; j <= last; j++)
                {
                    if (used[j]) continue;
                    if (!Unificator.TryMatch(left[i], right[j], out var match)) continue;

                    var nextLeft = left;
                    var nextRight = right;
                    if (!match.IsEmpty)
                    {
                        var substitution = new Substitution(match.Substitutions);
                        nextLeft = new List<ISentence>(left);
                        nextRight = new List<ISentence>(right);
                        for (var k = i + 1; k < left.Count; k++)
                            nextLeft[k] = substitution.Apply(left[k]);
                        for (var k = 0; k < right.Count; k++)
                            if (!used[k] && k != j)
                                nextRight[k] = substitution.Apply(right[k]);
                    }

                    used[j] = true;
                    if (MatchFrom(nextLeft, i + 1, nextRight, used)) return true;
                    used[j] = false;
                }

                return false;
            }
        }

        private static ISentence Conjoin(IReadOnlyList<ISentence> sentences) => sentences.ToList().ConnectSentences();

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) return false;
            return this.SequenceEqual((Theory)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                foreach (var sentence in this) hash = hash * 31 + sentence.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => string.Join("\n", this);
    }
}
