using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Goal-driven depth-first proof search with backtracking (cf. AIMA FOL-BC-ASK). Sound;
    // complete only for the function-free all-positive subset within the depth bound, which
    // guards against cyclic rules. Non-rule sentences in the KB are ignored.
    public class BackwardChaining
    {
        private static readonly HashSet<string> NoAbducibles = new();

        private readonly int _maxDepth;

        public BackwardChaining(int maxDepth = 500)
        {
            _maxDepth = maxDepth;
        }

        public bool Entails(IEnumerable<ISentence> kb, ISentence query)
        {
            if (!query.IsLiteral) return false;
            return Prove(Rule.FromAll(kb), new List<ISentence> { query },
                    Substitution.Empty, new List<ISentence>(),
                    0, new Counter(), NoAbducibles, _maxDepth)
                .Any();
        }

        // Every proof of `goals`, yielded as the literals it assumed along the way. A ground goal
        // over an abducible predicate may be assumed instead of proven.
        internal static IEnumerable<List<ISentence>> Prove(
            List<Rule> clauses, IReadOnlyList<ISentence> goals,
            Substitution theta, List<ISentence> assumed,
            int depth, Counter counter, HashSet<string> abducibles, int maxDepth)
        {
            if (goals.Count == 0)
            {
                yield return assumed;
                yield break;
            }
            if (depth > maxDepth) yield break;

            var goal = theta.Apply(goals[0]);
            var rest = goals.Skip(1).ToList();
            var sig = goal.Signature();

            foreach (var clause in clauses)
            {
                var fresh = clause.Renamed(counter.Next++);
                if (fresh.Head.Signature() != sig) continue;
                if (!Unificator.TryUnify(goal, fresh.Head, out var mgu)) continue;

                var subgoals = new List<ISentence>(fresh.Premises);
                subgoals.AddRange(rest);

                foreach (var solution in Prove(clauses, subgoals, theta.Extend(mgu),
                             assumed, depth + 1, counter, abducibles, maxDepth))
                    yield return solution;
            }

            if (!abducibles.Contains(goal.SymbolOf()) || !goal.IsGround()) yield break;
            if (assumed.Any(a => a.IsNegationOf(goal))) yield break;

            var extended = assumed.Any(a => a.Equals(goal))
                ? assumed
                : new List<ISentence>(assumed) { goal };

            foreach (var solution in Prove(clauses, rest, theta, extended,
                         depth + 1, counter, abducibles, maxDepth))
                yield return solution;
        }

        // A holder rather than a ref int because iterator methods cannot take by-reference parameters.
        internal sealed class Counter
        {
            public int Next;
        }
    }
}
