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
        public static List<ISentence> Saturate(IEnumerable<ISentence> kb) =>
            Saturate(kb, Enumerable.Empty<Term>());

        // `extraConstants` widens the grounding universe for the per-instance NAF fallback below;
        // Entails passes the query's constants so instances only mentioning them are reachable.
        private static List<ISentence> Saturate(IEnumerable<ISentence> kb, IEnumerable<Term> extraConstants)
        {
            var allRules = Rule.FromAll(kb);
            var rules = allRules.Where(c => !c.IsFact).ToList();

            var known = new HashSet<ISentence>();
            foreach (var fact in allRules.Where(c => c.IsFact))
            {
                known.Add(Canonical(fact.Head));
            }

            var constants = allRules
                .SelectMany(c => c.Premises.Concat(c.NafPremises).Append(c.Head))
                .SelectMany(ConstantsOf)
                .Concat(extraConstants)
                .Distinct()
                .ToList();

            var counter = new BackwardChaining.Counter();

            // A round reads a snapshot frozen at its start: `known` grows while Match lazily walks
            // `facts`, and a round-stable view is what gives the NAF tests below a fixed world to
            // fail against. `known` only ever grows, so carrying the new facts over at the end of a
            // round reconstructs the next round's snapshot without re-copying the whole set.
            var facts = known.ToList();
            var derived = new List<ISentence>();
            foreach (var stratum in Stratify(rules))
            {
                bool added;
                do
                {
                    added = false;
                    derived.Clear();
                    foreach (var rule in stratum)
                    {
                        var fresh = rule.Renamed(counter.Next++);
                        var matches = Match(fresh.Premises, 0, Substitution.Empty, facts, counter);
                        foreach (var theta in matches)
                        {
                            // NAF fails when any instance is derivable — Holds unifies, so a
                            // variable left free under NAF reads "no derivable instance".
                            var nafs = fresh.NafPremises.Select(theta.Apply).ToList();
                            var head = theta.Apply(fresh.Head);
                            if (nafs.All(naf => !Holds(facts, naf)))
                            {
                                var canonical = Canonical(head);
                                if (known.Add(canonical))
                                {
                                    derived.Add(canonical);
                                    added = true;
                                }
                                continue;
                            }

                            // A derivable instance only defeats itself when the NAF variable is
                            // shared with the rest of the rule instance (a universal fact bound
                            // it): the other instances stay entailed, so re-test per ground
                            // instance over the constant universe. Variables occurring only
                            // under NAF keep the ∄ reading and stay free.
                            var scope = fresh.Premises.Select(theta.Apply)
                                .SelectMany(p => p.VariablesOf())
                                .Concat(head.VariablesOf())
                                .ToHashSet();
                            var toGround = nafs.SelectMany(n => n.VariablesOf())
                                .Where(scope.Contains)
                                .Distinct()
                                .ToList();
                            if (toGround.Count == 0)
                            {
                                continue;
                            }

                            foreach (var grounding in Groundings(toGround, constants))
                            {
                                if (nafs.Any(naf => Holds(facts, grounding.Apply(naf))))
                                {
                                    continue;
                                }

                                var canonical = Canonical(grounding.Apply(head));
                                if (known.Add(canonical))
                                {
                                    derived.Add(canonical);
                                    added = true;
                                }
                            }
                        }
                    }
                    facts.AddRange(derived);
                }
                while (added);
            }

            return known.ToList();
        }

        private static IEnumerable<Substitution> Groundings(
            IReadOnlyList<Variable> variables, IReadOnlyList<Term> constants)
        {
            if (variables.Count == 0)
            {
                yield return Substitution.Empty;
                yield break;
            }

            var rest = variables.Skip(1).ToList();
            foreach (var constant in constants)
            {
                foreach (var grounding in Groundings(rest, constants))
                {
                    yield return grounding.Extend(
                        new Dictionary<Variable, Term> { [variables[0]] = constant });
                }
            }
        }

        internal static IEnumerable<Term> ConstantsOf(ISentence literal)
        {
            if (literal.AtomOf() is not IPredicate predicate)
            {
                yield break;
            }

            foreach (var term in predicate.Terms)
            {
                foreach (var constant in ConstantsIn(term))
                {
                    yield return constant;
                }
            }
        }

        private static IEnumerable<Term> ConstantsIn(Term term)
        {
            switch (term)
            {
                case Variable:
                    yield break;
                case Function { Arity: > 0 } function:
                    foreach (var arg in function.Terms)
                    {
                        foreach (var constant in ConstantsIn(arg))
                        {
                            yield return constant;
                        }
                    }
                    yield break;
                default:
                    yield return term;
                    yield break;
            }
        }

        // Alpha-renames a fact's variables to first-occurrence order ($0, $1, …; '$' is
        // unparseable, so user symbols cannot collide) so `known` deduplicates the variants
        // that per-use renaming would otherwise mint forever.
        private static ISentence Canonical(ISentence literal)
        {
            var next = 0;
            return literal.Renamed(_ => new Variable("$" + next++));
        }

        // Standardize apart per use, like BackwardChaining does per clause: a non-ground fact is
        // universally quantified, so each match must see fresh variables — otherwise a later
        // premise binds the same fact variable again and Extend's conflict-free contract breaks
        // (unsound heads, lost entailments, cyclic substitutions).
        private static ISentence RenamedApart(ISentence fact, int id)
        {
            return fact.Renamed(v => new Variable(v.TermSymbol + "#" + id));
        }

        public static bool Entails(IEnumerable<ISentence> kb, ISentence query) =>
            query.IsLiteral && Holds(Saturate(kb, ConstantsOf(query)), query);

        public static bool Holds(IReadOnlyList<ISentence> facts, ISentence query)
        {
            if (!query.IsLiteral)
            {
                throw new ArgumentException($"Holds is literal-only; got non-literal query '{query}'.");
            }

            var sig = query.PolaritySignature();
            return facts.Any(f => f.PolaritySignature() == sig &&
                                  Unificator.TryUnify(query, StandardizedApart(f, query), out _));
        }

        // Holds' enumerating sibling: one binding set per fact the query unifies with, keyed on
        // the query's variables. A ground query yields one empty binding set per matching fact.
        public static List<Dictionary<Variable, Term>> Answers(IReadOnlyList<ISentence> facts, ISentence query)
        {
            if (!query.IsLiteral)
            {
                throw new ArgumentException($"Answers is literal-only; got non-literal query '{query}'.");
            }

            var sig = query.PolaritySignature();
            var queryVariables = query.VariablesOf().ToList();
            var answers = new List<Dictionary<Variable, Term>>();
            foreach (var fact in facts)
            {
                if (fact.PolaritySignature() != sig)
                {
                    continue;
                }

                if (!Unificator.TryUnify(query, StandardizedApart(fact, query), out var mgu))
                {
                    continue;
                }

                var substitution = new Substitution(mgu);
                var bindings = new Dictionary<Variable, Term>();
                foreach (var variable in queryVariables)
                {
                    var resolved = substitution.Walk(variable);
                    if (!resolved.Equals(variable))
                    {
                        bindings.Add(variable, resolved);
                    }
                }
                answers.Add(bindings);
            }
            return answers;
        }

        // A fact is universally quantified, so a variable name it shares with the query is a
        // coincidence, not a constraint — rename the collision away before unifying.
        private static ISentence StandardizedApart(ISentence fact, ISentence query)
        {
            var taken = query.VariablesOf().Select(v => v.TermSymbol).ToHashSet();
            var next = 0;
            return fact.Renamed(v => taken.Contains(v.TermSymbol) ? new Variable("$q" + next++) : null);
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
            int StratumOf(ISentence literal) => strata.GetValueOrDefault(literal.PolaritySignature(), 0);

            var headCount = rules.Select(r => r.Head.PolaritySignature()).Distinct().Count();
            bool changed;
            do
            {
                changed = false;
                foreach (var rule in rules)
                {
                    var required = 0;
                    foreach (var p in rule.Premises)
                    {
                        required = Math.Max(required, StratumOf(p));
                    }

                    foreach (var n in rule.NafPremises)
                    {
                        required = Math.Max(required, StratumOf(n) + 1);
                    }

                    var key = rule.Head.PolaritySignature();
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

            return rules.GroupBy(r => strata.GetValueOrDefault(r.Head.PolaritySignature(), 0))
                .OrderBy(g => g.Key)
                .Select(g => g.ToList())
                .ToList();
        }

        private static IEnumerable<Substitution> Match(
            IReadOnlyList<ISentence> premises, int index,
            Substitution theta, List<ISentence> facts, BackwardChaining.Counter counter)
        {
            if (index == premises.Count)
            {
                yield return theta;
                yield break;
            }

            var goal = theta.Apply(premises[index]);
            foreach (var fact in facts)
            {
                if (!Unificator.TryMatch(goal, RenamedApart(fact, counter.Next++), out var match))
                {
                    continue;
                }

                var extended = theta.Extend(match.Substitutions);
                foreach (var solution in Match(premises, index + 1, extended, facts, counter))
                {
                    yield return solution;
                }
            }
        }
    }
}
