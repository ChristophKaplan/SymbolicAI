using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Data-driven rule firing to fixpoint (cf. AIMA FOL-FC-ASK, generalized to literal heads and
    // premises). Sound; complete only for the function-free all-positive subset — no case analysis,
    // no ex falso (q and ¬q may coexist in the closure). Non-rule sentences in the KB are ignored.
    public class ForwardChaining
    {
        public List<ISentence> Saturate(IEnumerable<ISentence> kb)
        {
            var clauses = kb.Select(Rule.From).Where(c => c != null).Select(c => c!).ToList();
            var rules = clauses.Where(c => !c.IsFact).ToList();

            var known = new HashSet<ISentence>();
            foreach (var fact in clauses.Where(c => c.IsFact)) known.Add(fact.Head);

            var rename = 0;
            bool added;
            do
            {
                added = false;
                // New facts enter `known` immediately but are matched against only from the next
                // round; this snapshot keeps each round well-defined and reaches the same fixpoint.
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

        // A query with variables is entailed when some inferred literal is an instance of it.
        public bool Entails(IEnumerable<ISentence> kb, ISentence query)
        {
            if (!query.IsLiteral) return false;
            var sig = Bindings.Signature(query);
            return Saturate(kb).Any(fact =>
                Bindings.Signature(fact) == sig && Bindings.TryUnify(query, fact, out _));
        }

        // Conjunctive join over the fact base: every substitution extending `theta` under which all
        // premises from `index` onward hold.
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
