using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Forward chaining (data-driven) over definite clauses: repeatedly fire every rule whose premises
    // are satisfied by the known facts, adding each inferred head, until a round adds nothing new
    // (fixpoint). Sound; for function-free (Datalog) clause sets also complete and terminating. With
    // function symbols the Herbrand base can be infinite and saturation may not terminate — the same
    // caveat Resolution carries on non-ground input. Only the definite-clause subset of the knowledge
    // base participates; sentences outside it are ignored. Cf. AIMA FOL-FC-ASK.
    public class ForwardChaining
    {
        // The deductive closure: every positive atom entailed by the definite-clause subset of `kb`,
        // grown from its facts.
        public List<ISentence> Saturate(IEnumerable<ISentence> kb)
        {
            var clauses = kb.Select(DefiniteClause.From).Where(c => c != null).Select(c => c!).ToList();
            var rules = clauses.Where(c => !c.IsFact).ToList();

            var known = new HashSet<ISentence>();
            foreach (var fact in clauses.Where(c => c.IsFact)) known.Add(fact.Head);

            var rename = 0;
            bool added;
            do
            {
                added = false;
                // New facts enter `known` immediately but are matched against only from the next round;
                // this snapshot keeps each round's work well-defined and reaches the same fixpoint.
                var facts = known.ToList();
                foreach (var rule in rules)
                {
                    var fresh = rule.Renamed(rename++);
                    foreach (var theta in Match(fresh.Premises, 0, new Dictionary<Variable, Term>(), facts))
                    {
                        var head = Bindings.Apply(fresh.Head, theta);
                        if (known.Add(head)) added = true;
                    }
                }
            }
            while (added);

            return known.ToList();
        }

        // True iff the definite-clause subset of `kb` entails `query` (a positive literal). A query with
        // variables is entailed when some inferred fact is an instance of it. Horn knowledge bases
        // derive only positive atoms, so a negated or complex query is never entailed here.
        public bool Entails(IEnumerable<ISentence> kb, ISentence query)
        {
            if (!query.IsLiteral || query.IsNegation) return false;
            var sig = Bindings.Signature(query);
            return Saturate(kb).Any(fact =>
                Bindings.Signature(fact) == sig && Bindings.TryUnify(query, fact, out _));
        }

        // Enumerate every substitution extending `theta` under which all premises from `index` onward
        // hold against `facts`: ground the next premise by θ-so-far, unify it with each candidate fact,
        // and recurse on the matches (a conjunctive join over the fact base).
        private static IEnumerable<Dictionary<Variable, Term>> Match(
            IReadOnlyList<ISentence> premises, int index,
            Dictionary<Variable, Term> theta, List<ISentence> facts)
        {
            if (index == premises.Count)
            {
                yield return theta;
                yield break;
            }

            var goal = Bindings.Apply(premises[index], theta);
            var sig = Bindings.Signature(goal);
            foreach (var fact in facts)
            {
                if (Bindings.Signature(fact) != sig) continue;
                if (!Bindings.TryUnify(goal, fact, out var mgu)) continue;
                var extended = Bindings.Extend(theta, mgu);
                foreach (var solution in Match(premises, index + 1, extended, facts))
                    yield return solution;
            }
        }
    }
}
