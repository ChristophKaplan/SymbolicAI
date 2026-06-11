using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public class Resolution
    {
        private static readonly FirstOrderLogic Logic = new();

        // Subsumption (unit-clause based) is sound but its bookkeeping only pays off on larger,
        // redundancy-heavy problems; on small/medium inputs it can cost more than it saves. It is
        // therefore opt-in and off by default. Tautology elimination and factoring are always on.
        private readonly bool _useSubsumption;

        public Resolution(bool useSubsumption = false)
        {
            _useSubsumption = useSubsumption;
        }

        private List<Resolvent> GetResolvents(Clause clause1, Clause clause2)
        {
            var resolvents = new List<Resolvent>();

            // i indexes clause1's literals, j indexes clause2's — independent lists, so j starts at 0.
            for (var i = 0; i < clause1.Literals.Count; i++)
            {
                for (var j = 0; j < clause2.Literals.Count; j++)
                {
                    var lit1 = clause1.Literals[i];
                    var lit2 = clause2.Literals[j];

                    // Resolution needs one positive and one negative literal.
                    if (lit1.IsNegation == lit2.IsNegation) continue;

                    var unify = new Unificator(lit1, lit2);
                    if (!unify.IsUnifiable) continue;

                    // Clone only the kept literals (the resolved-on pair is dropped anyway) and
                    // substitute on those clones, so the source clauses are never mutated in place.
                    var kept = new List<ISentence>(clause1.Literals.Count + clause2.Literals.Count - 2);
                    for (var k = 0; k < clause1.Literals.Count; k++)
                        if (k != i) kept.Add(clause1.Literals[k].Clone());
                    for (var k = 0; k < clause2.Literals.Count; k++)
                        if (k != j) kept.Add(clause2.Literals[k].Clone());

                    foreach (var pair in unify.Substitutions)
                        foreach (var literal in kept)
                            literal.SubstituteTerm(pair.Key, pair.Value);

                    // Factoring: collapse literals that became identical after substitution, matching
                    // the set semantics the resolver relies on for dedup/termination.
                    var res = new List<ISentence>(kept.Count);
                    foreach (var literal in kept)
                        if (!res.Contains(literal)) res.Add(literal);

                    resolvents.Add(new Resolvent(clause1, clause2, res.ToArray()));
                }
            }

            return resolvents;
        }

        public bool Resolve(ISentence knowledgeBase, ISentence consequence)
        {
            ISentence joined = new ComplexSentence(
                knowledgeBase, Connective.LogicSymbol.CONJUNCTION, consequence.Negate());

            // The negated consequence may be a complex (non-CNF) sentence, so normalize the whole
            // refutation set to CNF before clausifying it.
            if (!joined.IsCNF())
            {
                joined = Logic.ToConjunctiveNormalForm(joined);
            }

            var clauses = joined.GetClauseSet();

            // A clause holding both a literal and its negation is a tautology (always true): it can
            // never contribute to deriving the empty clause, so it is pure overhead. Dropping it is
            // sound and complete and shrinks every subsequent round.
            clauses.RemoveAll(IsTautology);

            var seen = new HashSet<Clause>(clauses, ClauseByContent);

            // Unit-clause index for cheap forward subsumption: a unit {L} subsumes every clause that
            // contains L, so a single hash lookup per literal replaces an O(n) scan over all clauses.
            // Units are the strongest and most common subsumers; restricting the per-resolvent check
            // to them keeps it cheap while backward subsumption (below) still uses the general rule.
            var unitLiterals = new HashSet<ISentence>();
            if (_useSubsumption)
                foreach (var clause in clauses)
                    if (clause.Literals.Count == 1) unitLiterals.Add(clause.Literals[0]);

            // resolvedUpTo marks the prefix of `clauses` whose mutual pairs have already been
            // resolved in a previous round. Re-resolving those pairs every round is pure waste
            // (their resolvents are already in `seen`), so each round only considers pairs where
            // at least one clause is new since the last round.
            var resolvedUpTo = 0;

            while (true)
            {
                var count = clauses.Count;
                var unitsBefore = unitLiterals.Count;
                var fresh = new List<Clause>();
                for (var i = 0; i < count; i++)
                {
                    // For an "old" clause (i < resolvedUpTo) only pair it with clauses added since
                    // the last round (j >= resolvedUpTo); old-old pairs were handled already.
                    // For a "new" clause start at j = i + 1: each unordered pair once, never with
                    // itself (self-resolution on complementary literals is unsound).
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

                            // Forward subsumption (cheap, unit-only): discard a resolvent that some
                            // existing unit clause already implies. We only drop the *new* clause and
                            // never remove kept ones, so the resolvedUpTo watermark stays valid.
                            if (_useSubsumption && IsUnitSubsumed(resolvent, unitLiterals)) continue;

                            if (!seen.Add(resolvent)) continue;
                            fresh.Add(resolvent);
                            if (_useSubsumption && resolvent.Literals.Count == 1)
                                unitLiterals.Add(resolvent.Literals[0]);
                        }
                    }
                }

                // No new clauses this round → the set is saturated, so the consequence is not entailed.
                // Dedup is by literal content, not Clause identity: each round mints fresh Resolvent
                // objects, so identity comparison would never saturate and the loop would never end.
                if (fresh.Count == 0) return false;

                // Backward subsumption (unit-only): drop any non-unit clause that a unit clause now
                // subsumes (i.e. it contains that unit's literal). Only a *newly* discovered unit can
                // make a previously kept clause redundant, so when no unit appeared this round we skip
                // the whole scan and just append — keeping subsumption near-free on unit-less rounds.
                if (unitLiterals.Count == unitsBefore)
                {
                    resolvedUpTo = count;
                    clauses.AddRange(fresh);
                }
                else
                {
                    // Surviving kept clauses preserve their order and are still pairwise resolved
                    // (removal never creates a new unresolved pair), so resolvedUpTo is just their new
                    // count; the surviving fresh clauses follow as the next round's "new" work.
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

        // True iff the clause contains some literal alongside its complement (e.g. P and ¬P),
        // making the whole disjunction valid. Such clauses are redundant for resolution.
        private static bool IsTautology(Clause clause)
        {
            var literals = clause.Literals;
            for (var i = 0; i < literals.Count; i++)
                for (var j = i + 1; j < literals.Count; j++)
                    if (literals[i].IsNegationOf(literals[j])) return true;
            return false;
        }

        // Syntactic (subset) subsumption restricted to unit subsumers: a unit clause {L} subsumes any
        // clause that contains L (since {L} ⊨ that clause), making the larger clause redundant.
        // This is the substitution-free special case (θ = identity) — conservative but always sound.
        // True iff some existing unit clause {L} subsumes `candidate`, i.e. candidate contains L.
        private static bool IsUnitSubsumed(Clause candidate, HashSet<ISentence> unitLiterals)
        {
            if (unitLiterals.Count == 0) return false;
            foreach (var literal in candidate.Literals)
                if (unitLiterals.Contains(literal)) return true;
            return false;
        }

        private static readonly IEqualityComparer<Clause> ClauseByContent = new ClauseContentComparer();

        // A clause is a set of literals: two clauses are equal iff they carry the same literals,
        // order aside. Built on ISentence value-equality.
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