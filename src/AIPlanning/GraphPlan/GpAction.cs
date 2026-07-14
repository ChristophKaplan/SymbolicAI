using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FirstOrderLogic;

namespace AIPlanning.Planning.GraphPlan {
    public class GpAction : IEquatable<GpAction> {
        private readonly int _hashcode;
        private readonly HashSet<Unificator> _unificators = new();
        public string Signifier { get; }
        public IReadOnlyList<ISentence> Preconditions { get; }
        public IReadOnlyList<ISentence> Effects { get; }
        public IReadOnlyCollection<Unificator> Unificators => _unificators;

        // Marks the injected Start/Finish actions, which must never surface at runtime.
        // A flag rather than reference identity, which a Clone would silently lose.
        public bool IsSynthetic { get; }

        public GpAction(string name, List<ISentence> preconditions, List<ISentence> effects, bool isSynthetic = false)
        {
            Signifier = name;
            Preconditions = preconditions;
            Effects = effects;
            IsSynthetic = isSynthetic;
            _hashcode = ComputeHashCode();
        }

        public GpAction Clone() => new GpAction(Signifier, Preconditions.ToList(), Effects.ToList(), IsSynthetic);

        // Belief-state matching is exact over ground literals, so only a fully ground action can fire.
        public bool IsGround() => Preconditions.All(p => p.IsGround()) && Effects.All(e => e.IsGround());

        public bool AddUnificators(IEnumerable<Unificator> unificators)
        {
            var added = false;
            foreach (var unificator in unificators)
            {
                added |= _unificators.Add(unificator);
            }

            return added;
        }

        public bool IsApplicableToPreconditions(GpBeliefState beliefState, [NotNullWhen(true)] out List<GpLiteralNode>? satisfied) {
            // Duplicate precondition literals map onto one node; comparing against the raw count
            // would make the action permanently inapplicable.
            var distinct = Preconditions.Distinct().ToList();
            satisfied = beliefState.GetSubSetOfNodesMatching(distinct);
            return satisfied != null && satisfied.Count == distinct.Count;
        }

        public HashSet<Unificator> GetConflictFreeUnificatorPossibilities() {
            var substitutions = ArrangeSubstitutionsAsTrees(_unificators);

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

        private static Dictionary<Variable, List<Term>> ArrangeSubstitutionsAsTrees(HashSet<Unificator> unificators) {
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

        // Returns a new instance: the hash covers the literals, and actions live in hash-keyed
        // collections — substituting in place would corrupt those.
        public GpAction SpecifyAction(Unificator unificator) {
            return new GpAction(Signifier,
                Preconditions.Select(unificator.Apply).ToList(),
                Effects.Select(unificator.Apply).ToList(),
                IsSynthetic);
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
        private int ComputeHashCode()
        {
            static int SumHashes(IReadOnlyList<ISentence> sentences) {
                var hash = 0;
                unchecked {
                    foreach (var sentence in sentences) {
                        hash += sentence.GetHashCode();
                    }
                }

                return hash;
            }

            return HashCode.Combine(Signifier, Preconditions.Count, Effects.Count, SumHashes(Preconditions), SumHashes(Effects));
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
