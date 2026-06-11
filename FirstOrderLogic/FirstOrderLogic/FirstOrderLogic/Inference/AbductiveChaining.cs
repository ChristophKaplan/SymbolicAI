using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Abduction over rules: the explanations of an observation are the minimal sets of assumed
    // ground literals (over the declared abducible predicates) that make the observation derivable.
    // BackwardChaining's proof search plus one extra move: assume a goal instead of proving it.
    // An explanation is discarded when adding it to the KB introduces a complementary literal pair
    // the KB's own closure does not already contain.
    public class AbductiveChaining
    {
        private readonly int _maxDepth;

        public AbductiveChaining(int maxDepth = 500)
        {
            _maxDepth = maxDepth;
        }

        public List<List<ISentence>> Explain(
            IEnumerable<ISentence> kb, ISentence observation, IEnumerable<string> abduciblePredicates)
        {
            if (!observation.IsLiteral) return new List<List<ISentence>>();

            var sentences = kb.ToList();
            var clauses = sentences.Select(Rule.From).Where(c => c != null).Select(c => c!).ToList();
            var abducibles = new HashSet<string>(abduciblePredicates);

            var candidates = Prove(
                    clauses, new List<ISentence> { observation }, new Dictionary<Variable, Term>(),
                    new List<ISentence>(), 0, new Counter(), abducibles)
                .ToList();

            var chaining = new ForwardChaining();
            var baseline = Contradictions(chaining.Saturate(sentences));

            var consistent = candidates
                .Where(h => h.Count == 0 ||
                            !Contradictions(chaining.Saturate(sentences.Concat(h))).Except(baseline).Any())
                .ToList();

            return Minimal(consistent);
        }

        private IEnumerable<List<ISentence>> Prove(
            List<Rule> clauses, IReadOnlyList<ISentence> goals,
            Dictionary<Variable, Term> theta, List<ISentence> assumed,
            int depth, Counter counter, HashSet<string> abducibles)
        {
            if (goals.Count == 0)
            {
                yield return assumed;
                yield break;
            }
            if (depth > _maxDepth) yield break;

            var goal = Bindings.Apply(goals[0], theta);
            var rest = goals.Skip(1).ToList();
            var sig = Bindings.Signature(goal);

            foreach (var clause in clauses)
            {
                var fresh = clause.Renamed(counter.Next++);
                if (Bindings.Signature(fresh.Head) != sig) continue;
                if (!Bindings.TryUnify(goal, fresh.Head, out var mgu)) continue;

                var theta2 = Bindings.Extend(theta, mgu);
                var subgoals = new List<ISentence>(fresh.Premises);
                subgoals.AddRange(rest);

                foreach (var solution in Prove(clauses, subgoals, theta2, assumed, depth + 1, counter, abducibles))
                    yield return solution;
            }

            // The abductive move: a ground goal over an abducible predicate may be assumed.
            if (!abducibles.Contains(SymbolOf(goal)) || !goal.IsGround()) yield break;
            if (assumed.Any(a => a.IsNegationOf(goal))) yield break;

            var extended = assumed.Any(a => a.Equals(goal))
                ? assumed
                : new List<ISentence>(assumed) { goal };

            foreach (var solution in Prove(clauses, rest, theta, extended, depth + 1, counter, abducibles))
                yield return solution;
        }

        private static string SymbolOf(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            return atom is IPredicate predicate ? predicate.Symbol : ((IAtomicSentence)atom).Symbol;
        }

        // Canonical keys of every complementary literal pair in `closure`.
        private static HashSet<string> Contradictions(List<ISentence> closure)
        {
            var pairs = new HashSet<string>();
            for (var i = 0; i < closure.Count; i++)
                for (var j = i + 1; j < closure.Count; j++)
                    if (closure[i].IsNegationOf(closure[j]))
                        pairs.Add(closure[i].IsNegation ? closure[j].ToString() : closure[i].ToString());
            return pairs;
        }

        private static List<List<ISentence>> Minimal(List<List<ISentence>> sets)
        {
            var keyed = sets
                .Select(s => (set: s, key: s.Select(x => x.ToString()).ToHashSet()))
                .ToList();

            var result = new List<List<ISentence>>();
            var emitted = new List<HashSet<string>>();
            foreach (var (set, key) in keyed.OrderBy(p => p.key.Count))
            {
                if (emitted.Any(e => e.IsSubsetOf(key))) continue;
                emitted.Add(key);
                result.Add(set);
            }
            return result;
        }

        private sealed class Counter
        {
            public int Next;
        }
    }
}
