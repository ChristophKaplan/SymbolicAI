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

        // Marks the injected Start/Finish actions, which must never surface at runtime.
        // A flag rather than reference identity, which a Clone would silently lose.
        public bool IsSynthetic { get; }

        private GpAction(GpAction action) : this(action.Signifier,
            action.Preconditions.ToList(),
            action.Effects.ToList(),
            action.IsSynthetic) {
        }

        public GpAction(string name, List<ISentence> preconditions, List<ISentence> effects, bool isSynthetic = false)
        {
            Signifier = name;
            Preconditions = preconditions;
            Effects = effects;
            IsSynthetic = isSynthetic;
            UpdateHashCode();
        }

        public GpAction Clone() => new GpAction(this);

        // Belief-state matching is exact over ground literals, so only a fully ground action can fire.
        public bool IsGround() => Preconditions.All(p => p.IsGround()) && Effects.All(e => e.IsGround());

        public void AddUnificators(IEnumerable<Unificator> unificators)
        {
            Unificators.UnionWith(unificators);
        }
    
        public bool IsApplicableToPreconditions(GpBeliefState beliefState, [NotNullWhen(true)] out List<GpNode>? satisfied) {
            // Duplicate precondition literals map onto one node; comparing against the raw count
            // would make the action permanently inapplicable.
            var distinct = Preconditions.Distinct().ToList();
            satisfied = beliefState.GetSubSetOfNodesMatching(distinct);
            return satisfied != null && satisfied.Count == distinct.Count;
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

                // Each unifier is acyclic on its own, but recombining bindings from different
                // unifiers can close a cycle (x→y from one, y→x from another), which
                // Substitution.Walk must never see.
                if (!IsAcyclic(possibility)) {
                    continue;
                }

                possibilities.Add(new Unificator(possibility));
            }

            return possibilities;
        }

        private static bool IsAcyclic(Dictionary<Variable, Term> substitution) {
            foreach (var start in substitution.Keys) {
                var seen = new HashSet<Variable>();
                var frontier = new Stack<Variable>();
                frontier.Push(start);
                while (frontier.Count > 0) {
                    if (!substitution.TryGetValue(frontier.Pop(), out var term)) {
                        continue;
                    }

                    foreach (var variable in term.GetVariables()) {
                        if (variable.Equals(start)) {
                            return false;
                        }

                        if (seen.Add(variable)) {
                            frontier.Push(variable);
                        }
                    }
                }
            }

            return true;
        }

        private Dictionary<Variable, List<Term>> ArrangeSubstitutionsAsTrees(HashSet<Unificator> unificators) {
            var collectPossibilities = new Dictionary<Variable, List<Term>>();

            foreach (var unificator in unificators) {
                if (unificator.IsEmpty) {
                    continue;
                }

                foreach (var substitution in unificator.Substitutions) {
                    if (!collectPossibilities.TryGetValue(substitution.Key, out var terms)) {
                        collectPossibilities.Add(substitution.Key, new List<Term> { substitution.Value });
                    }
                    else if (!terms.Contains(substitution.Value)) {
                        terms.Add(substitution.Value);
                    }
                }
            }

            return collectPossibilities;
        }

        public void SpecifyAction(Unificator unificator) {
            for (var i = 0; i < Preconditions.Count; i++) Preconditions[i] = unificator.Apply(Preconditions[i]);
            for (var i = 0; i < Effects.Count; i++) Effects[i] = unificator.Apply(Effects[i]);
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

        // Summing element hashes keeps duplicates visible ({P,P,Q} vs {P,Q,Q} differ);
        // XOR would let duplicate pairs cancel out.
        private void UpdateHashCode()
        {
            var preHash = 0;
            unchecked {
                foreach (var precondition in Preconditions)
                {
                    preHash += precondition.GetHashCode();
                }
            }

            var effHash = 0;
            unchecked {
                foreach (var effect in Effects)
                {
                    effHash += effect.GetHashCode();
                }
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

            if (Signifier != other.Signifier)
            {
                return false;
            }

            return Preconditions.MultisetEquals(other.Preconditions)
                && Effects.MultisetEquals(other.Effects);
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