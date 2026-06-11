using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public interface IGpAction {
        string Signifier { get; }
        List<ISentence> Preconditions { get; }
        List<ISentence> Effects { get; }
        bool IsApplicableToPreconditions(GpBeliefState beliefState, [NotNullWhen(true)] out List<GpNode>? satisfied);
    }

    public class GpAction : IGpAction , IEquatable<GpAction> {
        private int _hashcode;
        public string Signifier { get; }
        public List<ISentence> Preconditions { get; }
        public List<ISentence> Effects { get; }
        public HashSet<Unificator> Unificators { get; private set; } = new();

        private GpAction(GpAction action) : this(action.Signifier,
            action.Preconditions.Select(p => p.Clone()).ToList(),
            action.Effects.Select(e => e.Clone()).ToList()) {
        }

        public GpAction(string name, List<ISentence> preconditions, List<ISentence> effects)
        {
            Signifier = name;
            Preconditions = preconditions;
            Effects = effects;
            UpdateHashCode();
        }

        public GpAction Clone() => new GpAction(this);

        public void AddUnificators(IEnumerable<Unificator> unificators)
        {
            Unificators.UnionWith(unificators);
        }
    
        public bool IsApplicableToPreconditions(GpBeliefState beliefState, [NotNullWhen(true)] out List<GpNode>? satisfied) {
            satisfied = beliefState.GetSubSetOfNodesMatching(Preconditions);
            return satisfied != null && satisfied.Count == Preconditions.Count;
        }

        public HashSet<Unificator> GetConflictFreeUnificatorPossibilities(HashSet<Unificator> unificators) {
            var substitutions = ArrangeSubstitutionsAsTrees(unificators);

            var variables = substitutions.Keys.ToList();
            var termLists = substitutions.Values.ToList();
            var combs = termLists.GetCombinations();

            var possibilities = new HashSet<Unificator>();

            foreach (var comb in combs) {
                var possibility = new Dictionary<Variable, Term>();
                for (var i = 0; i < variables.Count; i++) {
                    possibility.Add(variables[i], comb[i]);
                }
            
                possibilities.Add(new Unificator(possibility));
            }

            return possibilities;
        }

        private Dictionary<Variable, List<Term>> ArrangeSubstitutionsAsTrees(HashSet<Unificator> unificators) {
            var collectPossibilities = new Dictionary<Variable, List<Term>>();

            foreach (var unificator in unificators) {
                if (unificator.IsEmpty) {
                    continue;
                }

                foreach (var substitution in unificator.Substitutions) {
                    if (!collectPossibilities.TryAdd(substitution.Key, new List<Term> { substitution.Value })) {
                        collectPossibilities[substitution.Key].Add(substitution.Value);
                    }
                }
            }

            return collectPossibilities;
        }

        public void SpecifyAction(Unificator unificator) {
            foreach (var preCon in Preconditions) unificator.Substitute(preCon);
            foreach (var effect in Effects) unificator.Substitute(effect);
            UpdateHashCode();
        }

        public bool IsConsistent() {
            var isConflictInPreCons = Preconditions.Any(p1 => Preconditions.Any(p2 => p1.IsNegationOf(p2)));
            var isConflictInEffects = Effects.Any(eff1 => Effects.Any(eff2 => eff1.IsNegationOf(eff2)));
            return !isConflictInPreCons && !isConflictInEffects;
        }

        public override int GetHashCode()
        {
            return _hashcode;
        }

        // Order-insensitive: preconditions/effects are conceptually sets.
        // XOR of element hashes preserves that.
        private void UpdateHashCode()
        {
            var preHash = 0;
            foreach (var precondition in Preconditions)
            {
                preHash ^= precondition.GetHashCode();
            }

            var effHash = 0;
            foreach (var effect in Effects)
            {
                effHash ^= effect.GetHashCode();
            }

            _hashcode = HashCode.Combine(Signifier, Preconditions.Count, Effects.Count, preHash, effHash);
        }

        public bool Equals(GpAction? other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (_hashcode != other._hashcode)
            {
                return false;
            }

            if (Signifier != other.Signifier ||
                Preconditions.Count != other.Preconditions.Count ||
                Effects.Count != other.Effects.Count)
            {
                return false;
            }

            // Set semantics on preconditions and effects (order-insensitive).
            foreach (var p in Preconditions)
            {
                if (!other.Preconditions.Contains(p)) return false;
            }

            foreach (var e in Effects)
            {
                if (!other.Effects.Contains(e)) return false;
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is GpAction other && Equals(other);
        }

        public override string ToString() {
            return $"{Signifier} {string.Join(",", Preconditions)} -> {string.Join(",", Effects)}";
        }
    }
}