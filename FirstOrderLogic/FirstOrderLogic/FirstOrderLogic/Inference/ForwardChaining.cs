using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Data-driven rule firing to a fixpoint (cf. AIMA FOL-FC-ASK). Complete only for the
    // function-free, range-restricted positive subset; unsafe rules are rejected by Rule.From.
    // NAF premises ("not derivable") are evaluated stratum by stratum: every predicate a rule
    // reads through NAF is fully saturated before the rule fires, so the closed-world answer
    // can no longer change.
    public class ForwardChaining
    {
        public static List<ISentence> Saturate(IEnumerable<ISentence> kb)
        {
            var clauses = Rule.FromAll(kb);
            var rules = clauses.Where(c => !c.IsFact).ToList();

            var known = new HashSet<ISentence>();
            foreach (var fact in clauses.Where(c => c.IsFact)) known.Add(Canonical(fact.Head));

            var rename = new BackwardChaining.Counter();
            foreach (var stratum in Stratify(rules))
            {
                bool added;
                do
                {
                    added = false;
                    var facts = known.ToList();
                    foreach (var rule in stratum)
                    {
                        var fresh = rule.Renamed(rename.Next++);
                        var matches = Match(fresh.Premises, 0, Substitution.Empty, facts, rename);
                        foreach (var theta in matches)
                        {
                            // NAF fails when any instance is derivable — Holds unifies, so a
                            // variable left free under NAF reads "no derivable instance".
                            if (fresh.NafPremises.Any(naf => Holds(facts, theta.Apply(naf)))) continue;
                            var head = Canonical(theta.Apply(fresh.Head));
                            if (known.Add(head)) added = true;
                        }
                    }
                }
                while (added);
            }

            return known.ToList();
        }

        // Alpha-renames a fact's variables to first-occurrence order ($0, $1, …; '$' is
        // unparseable, so user symbols cannot collide) so `known` deduplicates the variants
        // that per-use renaming would otherwise mint forever.
        private static ISentence Canonical(ISentence literal)
        {
            var next = 0;
            var result = literal;
            foreach (var variable in literal.VariablesOf().Distinct().ToList())
            {
                result = result.Substitute(variable, new Variable("$" + next++));
            }

            return result;
        }

        // Standardize apart per use, like BackwardChaining does per clause: a non-ground fact is
        // universally quantified, so each match must see fresh variables — otherwise a later
        // premise binds the same fact variable again and Extend's conflict-free contract breaks
        // (unsound heads, lost entailments, cyclic substitutions).
        private static ISentence RenamedApart(ISentence fact, int id)
        {
            var result = fact;
            foreach (var variable in fact.VariablesOf().Distinct().ToList())
            {
                result = result.Substitute(variable, new Variable(variable.TermSymbol + "#" + id));
            }

            return result;
        }

        public static bool Entails(IEnumerable<ISentence> kb, ISentence query) =>
            query.IsLiteral && Holds(Saturate(kb), query);

        public static bool Holds(IReadOnlyList<ISentence> facts, ISentence query)
        {
            if (!query.IsLiteral)
            {
                throw new System.ArgumentException($"Holds is literal-only; got non-literal query '{query}'.");
            }

            var sig = query.Signature();
            return facts.Any(f => f.Signature() == sig && Unificator.TryUnify(query, f, out _));
        }

        // Stratified evaluation: a rule runs strictly after every literal it reads through NAF is
        // complete. A head's stratum is the max over its positive dependencies, and over its NAF
        // dependencies + 1; a NAF cycle admits no such order and is rejected. Keys carry polarity
        // (Signature), so a rule may derive ¬P from NAF P.
        private static List<List<Rule>> Stratify(List<Rule> rules)
        {
            if (rules.All(r => r.NafPremises.Count == 0))
            {
                return rules.Count == 0 ? new List<List<Rule>>() : new List<List<Rule>> { rules };
            }

            var strata = new Dictionary<string, int>();
            int StratumOf(ISentence literal) => strata.GetValueOrDefault(literal.Signature(), 0);

            var headCount = rules.Select(r => r.Head.Signature()).Distinct().Count();
            bool changed;
            do
            {
                changed = false;
                foreach (var rule in rules)
                {
                    var required = 0;
                    foreach (var p in rule.Premises) required = Math.Max(required, StratumOf(p));
                    foreach (var n in rule.NafPremises) required = Math.Max(required, StratumOf(n) + 1);
                    var key = rule.Head.Signature();
                    if (required > strata.GetValueOrDefault(key, 0))
                    {
                        if (required > headCount)
                        {
                            throw new ArgumentException(
                                $"KB is not stratifiable: negation-as-failure cycle through '{rule}'.");
                        }
                        strata[key] = required;
                        changed = true;
                    }
                }
            }
            while (changed);

            return rules.GroupBy(r => strata.GetValueOrDefault(r.Head.Signature(), 0))
                .OrderBy(g => g.Key)
                .Select(g => g.ToList())
                .ToList();
        }

        private static IEnumerable<Substitution> Match(
            IReadOnlyList<ISentence> premises, int index,
            Substitution theta, List<ISentence> facts, BackwardChaining.Counter rename)
        {
            if (index == premises.Count)
            {
                yield return theta;
                yield break;
            }

            var goal = theta.Apply(premises[index]);
            foreach (var fact in facts)
            {
                if (!Unificator.TryMatch(goal, RenamedApart(fact, rename.Next++), out var match)) continue;
                var extended = theta.Extend(match.Substitutions);
                foreach (var solution in Match(premises, index + 1, extended, facts, rename))
                    yield return solution;
            }
        }
    }
}
