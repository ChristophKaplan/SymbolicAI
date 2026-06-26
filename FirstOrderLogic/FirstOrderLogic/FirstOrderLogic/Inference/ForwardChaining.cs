using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Data-driven rule firing to a fixpoint (cf. AIMA FOL-FC-ASK). Complete only for the
    // function-free, range-restricted positive subset; unsafe rules are rejected by Rule.From.
    public class ForwardChaining
    {
        public static List<ISentence> Saturate(IEnumerable<ISentence> kb)
        {
            var clauses = Rule.FromAll(kb);
            var rules = clauses.Where(c => !c.IsFact).ToList();

            var known = new HashSet<ISentence>();
            foreach (var fact in clauses.Where(c => c.IsFact)) known.Add(fact.Head);

            var rename = 0;
            bool added;
            do
            {
                added = false;
                var facts = known.ToList();
                foreach (var rule in rules)
                {
                    var fresh = rule.Renamed(rename++);
                    var matches = Match(fresh.Premises, 0, Substitution.Empty, facts);
                    foreach (var theta in matches)
                    {
                        var head = theta.Apply(fresh.Head);
                        if (known.Add(head)) added = true;
                    }
                }
            }
            while (added);

            return known.ToList();
        }

        public static bool Entails(IEnumerable<ISentence> kb, ISentence query) =>
            query.IsLiteral && Holds(Saturate(kb), query);

        public static bool Holds(IReadOnlyList<ISentence> facts, ISentence query)
        {
            var sig = query.Signature();
            return facts.Any(f => f.Signature() == sig && Unificator.TryUnify(query, f, out _));
        }

        private static IEnumerable<Substitution> Match(
            IReadOnlyList<ISentence> premises, int index,
            Substitution theta, List<ISentence> facts)
        {
            if (index == premises.Count)
            {
                yield return theta;
                yield break;
            }

            var goal = theta.Apply(premises[index]);
            foreach (var fact in facts)
            {
                if (!Unificator.TryMatch(goal, fact, out var match)) continue;
                var extended = theta.Extend(match.Substitutions);
                foreach (var solution in Match(premises, index + 1, extended, facts))
                    yield return solution;
            }
        }
    }
}
