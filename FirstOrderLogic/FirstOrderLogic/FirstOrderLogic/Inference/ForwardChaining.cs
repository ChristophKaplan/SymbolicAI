using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Data-driven rule firing to fixpoint (cf. AIMA FOL-FC-ASK). Sound; complete only for the
    // function-free all-positive subset. Non-rule sentences in the KB are ignored.
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
                // New facts are matched against only from the next round; same fixpoint.
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

        public static bool Entails(IEnumerable<ISentence> kb, ISentence query) =>
            query.IsLiteral && Holds(Saturate(kb), query);

        // Some fact is an instance of `query` (same polarity).
        public static bool Holds(IReadOnlyList<ISentence> facts, ISentence query)
        {
            var sig = Bindings.Signature(query);
            return facts.Any(f => Bindings.Signature(f) == sig && Bindings.TryUnify(query, f, out _));
        }

        // Every substitution extending `theta` under which all premises from `index` onward hold.
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
