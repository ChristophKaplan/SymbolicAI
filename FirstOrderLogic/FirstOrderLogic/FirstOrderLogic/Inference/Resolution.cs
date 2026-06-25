using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    
    public static class Resolution
    {
        private static readonly FirstOrderLogic Logic = new();

        private static List<Resolvent> GetResolvents(Clause clause1, Clause clause2)
        {
            var resolvents = new List<Resolvent>();

            // Standardize apart: clause variables are local, so a name shared across the two
            // clauses must be renamed before unifying or it acts as a spurious constraint.
            var literals2 = StandardizeApart(clause1.Literals, clause2.Literals);

            for (var i = 0; i < clause1.Literals.Count; i++)
            {
                for (var j = 0; j < literals2.Count; j++)
                {
                    var lit1 = clause1.Literals[i];
                    var lit2 = literals2[j];

                    if (lit1.IsNegation == lit2.IsNegation) continue;

                    var unify = new Unificator(lit1, lit2);
                    if (!unify.IsUnifiable) continue;

                    // Apply the mgu purely; each result is fresh, so the source clauses are never mutated.
                    var mgu = new Substitution(unify.Substitutions);
                    var kept = new List<ISentence>(clause1.Literals.Count + literals2.Count - 2);
                    for (var k = 0; k < clause1.Literals.Count; k++)
                        if (k != i) kept.Add(mgu.Apply(clause1.Literals[k]));
                    for (var k = 0; k < literals2.Count; k++)
                        if (k != j) kept.Add(mgu.Apply(literals2[k]));

                    // Factoring: collapse literals that became identical after substitution.
                    var res = new List<ISentence>(kept.Count);
                    foreach (var literal in kept)
                        if (!res.Contains(literal)) res.Add(literal);

                    // Canonicalize names so alpha-variant resolvents dedup in the seen-set;
                    // otherwise saturation is never detected and non-entailed queries loop forever.
                    CanonicalizeVariables(res);

                    resolvents.Add(new Resolvent(clause1, clause2, res.ToArray()));
                }
            }

            return resolvents;
        }

        // Clones of `right` with colliding variables renamed fresh; never mutates either input.
        // "y$" names cannot pre-exist ('$' is unparseable, resolvents are canonicalized to "x$"),
        // so a per-call counter suffices.
        private static List<ISentence> StandardizeApart(List<ISentence> left, List<ISentence> right)
        {
            var leftNames = new HashSet<string>();
            foreach (var literal in left)
                foreach (var variable in literal.VariablesOf())
                    leftNames.Add(variable.TermSymbol);

            if (leftNames.Count == 0) return right;

            var freshVarCounter = 0;
            var renames = new Dictionary<string, Variable>();
            foreach (var literal in right)
                foreach (var variable in literal.VariablesOf())
                    if (leftNames.Contains(variable.TermSymbol) && !renames.ContainsKey(variable.TermSymbol))
                        renames.Add(variable.TermSymbol, new Variable($"y${++freshVarCounter}"));

            if (renames.Count == 0) return right;

            var theta = new Dictionary<Variable, Term>(renames.Count);
            foreach (var pair in renames)
                theta.Add(new Variable(pair.Key), pair.Value);

            var substitution = new Substitution(theta);
            var renamed = new List<ISentence>(right.Count);
            foreach (var literal in right)
                renamed.Add(substitution.Apply(literal));

            return renamed;
        }

        // Renames variables in place to x$1, x$2, … by first occurrence over a name-insensitive
        // literal order, so alpha-variant clauses end up syntactically equal.
        private static void CanonicalizeVariables(List<ISentence> literals)
        {
            var ordered = literals
                .Select(literal => (literal, key: StructuralKey(literal)))
                .OrderBy(pair => pair.key, StringComparer.Ordinal);

            var canonical = new Dictionary<string, Variable>();
            foreach (var (literal, _) in ordered)
                foreach (var variable in literal.VariablesOf())
                    if (!canonical.ContainsKey(variable.TermSymbol))
                        canonical.Add(variable.TermSymbol, new Variable($"x${canonical.Count + 1}"));

            if (canonical.Count == 0) return;
            if (canonical.All(pair => pair.Key == pair.Value.TermSymbol)) return;

            // Two-phase rename via temporaries: source and target names may overlap.
            var temps = new Dictionary<string, Variable>();
            foreach (var name in canonical.Keys)
                temps.Add(name, new Variable($"t${temps.Count + 1}"));

            for (var k = 0; k < literals.Count; k++)
            {
                var literal = literals[k];
                foreach (var pair in temps)
                    literal = literal.Substitute(new Variable(pair.Key), pair.Value);
                foreach (var pair in temps)
                    literal = literal.Substitute(pair.Value, canonical[pair.Key]);
                literals[k] = literal;
            }
        }

        private static string StructuralKey(ISentence literal)
        {
            foreach (var variable in literal.VariablesOf().ToList())
                literal = literal.Substitute(variable, Placeholder);
            return literal.ToString();
        }

        private static readonly Variable Placeholder = new("$");

        // Refutation: KB ⊨ consequence iff KB ∧ ¬consequence is unsatisfiable.
        // useSubsumption is opt-in: it only pays off on larger, redundancy-heavy problems.
        // maxRounds (0 = unlimited) bounds the saturation loop (FOL entailment is only
        // semi-decidable) and throws when exceeded.
        public static bool Resolve(ISentence knowledgeBase, ISentence consequence,
            bool useSubsumption = false, int maxRounds = 0) =>
            IsUnsatisfiable(new ComplexSentence(
                knowledgeBase, Connective.LogicSymbol.CONJUNCTION, consequence.Negated()),
                useSubsumption, maxRounds);

        // True iff the clause set derives the empty clause, i.e. `sentence` has no model.
        public static bool IsUnsatisfiable(ISentence sentence, bool useSubsumption = false, int maxRounds = 0)
        {
            // Clausification needs a quantifier-free sentence: prenex, then skolemize. Runs after
            // Resolve's negation, so goal quantifiers are already flipped — the order that keeps
            // refutation sound.
            if (sentence.HasQuantifier())
            {
                sentence = Logic.SkolemForm(Logic.ToPrenexForm(sentence));
            }

            if (!sentence.IsCNF())
            {
                sentence = Logic.ToConjunctiveNormalForm(sentence);
            }

            var clauses = sentence.GetClauseSet();

            // Tautologies can never contribute to the empty clause; dropping them is sound.
            clauses.RemoveAll(IsTautology);

            var seen = new HashSet<Clause>(clauses, ClauseByContent);

            // Unit-clause index for cheap forward subsumption: a unit {L} subsumes every clause
            // containing L, so a hash lookup replaces an O(n) scan.
            var unitLiterals = new HashSet<ISentence>();
            if (useSubsumption)
                foreach (var clause in clauses)
                    if (clause.Literals.Count == 1) unitLiterals.Add(clause.Literals[0]);

            // resolvedUpTo marks the prefix of `clauses` whose mutual pairs were already resolved;
            // each round only considers pairs where at least one clause is new.
            var resolvedUpTo = 0;
            var rounds = 0;

            while (true)
            {
                if (maxRounds > 0 && ++rounds > maxRounds)
                    throw new InvalidOperationException(
                        $"Resolution did not saturate within {maxRounds} rounds; the query is undecided.");

                var count = clauses.Count;
                var unitsBefore = unitLiterals.Count;
                var fresh = new List<Clause>();
                for (var i = 0; i < count; i++)
                {
                    // Each unordered pair once, skipping old-old pairs and self-pairing
                    // (self-resolution is unsound).
                    var j = i < resolvedUpTo ? resolvedUpTo : i + 1;
                    for (; j < count; j++)
                    {
                        var possibleResolvents = GetResolvents(clauses[i], clauses[j]);
                        if (possibleResolvents.Any(resolvent => resolvent.IsEmptyClause()))
                        {
                            return true;
                        }

                        foreach (var resolvent in possibleResolvents)
                        {
                            if (IsTautology(resolvent)) continue;

                            // Forward subsumption drops only the new clause, never kept ones, so
                            // the resolvedUpTo watermark stays valid.
                            if (useSubsumption && IsUnitSubsumed(resolvent, unitLiterals)) continue;

                            if (!seen.Add(resolvent)) continue;
                            fresh.Add(resolvent);
                            if (useSubsumption && resolvent.Literals.Count == 1)
                                unitLiterals.Add(resolvent.Literals[0]);
                        }
                    }
                }

                // No new clauses → saturated → not entailed.
                if (fresh.Count == 0) return false;

                // Backward subsumption: only a newly discovered unit can make a kept clause
                // redundant, so unit-less rounds skip the scan.
                if (unitLiterals.Count == unitsBefore)
                {
                    resolvedUpTo = count;
                    clauses.AddRange(fresh);
                }
                else
                {
                    // Survivors keep their order and stay pairwise-resolved, so resolvedUpTo is
                    // just their count.
                    var survivors = new List<Clause>(clauses.Count + fresh.Count);
                    foreach (var kept in clauses)
                        if (kept.Literals.Count == 1 || !IsUnitSubsumed(kept, unitLiterals))
                            survivors.Add(kept);

                    resolvedUpTo = survivors.Count;

                    foreach (var clause in fresh)
                        if (clause.Literals.Count == 1 || !IsUnitSubsumed(clause, unitLiterals))
                            survivors.Add(clause);

                    clauses = survivors;
                }
            }
        }

        private static bool IsTautology(Clause clause)
        {
            var literals = clause.Literals;
            for (var i = 0; i < literals.Count; i++)
                for (var j = i + 1; j < literals.Count; j++)
                    if (literals[i].IsNegationOf(literals[j])) return true;
            return false;
        }

        // Substitution-free (θ = identity) unit subsumption — conservative but always sound.
        private static bool IsUnitSubsumed(Clause candidate, HashSet<ISentence> unitLiterals)
        {
            if (unitLiterals.Count == 0) return false;
            foreach (var literal in candidate.Literals)
                if (unitLiterals.Contains(literal)) return true;
            return false;
        }

        private static readonly IEqualityComparer<Clause> ClauseByContent = new ClauseContentComparer();

        // Set equality over literals, order aside, on ISentence value-equality.
        private sealed class ClauseContentComparer : IEqualityComparer<Clause>
        {
            public bool Equals(Clause a, Clause b)
            {
                if (ReferenceEquals(a, b)) return true;
                if (a is null || b is null || a.Literals.Count != b.Literals.Count) return false;
                return a.Literals.All(literal => b.Literals.Contains(literal));
            }

            public int GetHashCode(Clause clause)
            {
                var hash = 0;
                foreach (var literal in clause.Literals) hash ^= literal?.GetHashCode() ?? 0;
                return hash;
            }
        }
    }
}