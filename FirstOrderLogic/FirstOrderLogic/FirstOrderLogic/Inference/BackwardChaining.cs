using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Goal-driven proof search, depth-first with backtracking — the Prolog strategy (cf. AIMA
    // FOL-BC-ASK, generalized to literal heads and premises). Sound; complete only for the
    // function-free all-positive subset within the depth bound. The depth limit guards against
    // left-recursive/cyclic rules (exceeding it prunes the branch rather than reporting
    // "not entailed"). Non-rule sentences in the KB are ignored.
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
                    new Dictionary<Variable, Term>(), new List<ISentence>(),
                    0, new Counter(), NoAbducibles, _maxDepth)
                .Any();
        }

        // Every proof of `goals` (a conjunction, proved left-to-right), yielded as the literals it
        // assumed along the way. A ground goal over an abducible predicate may be assumed instead
        // of proven — with no abducibles this is plain backward chaining and yields empty lists.
        internal static IEnumerable<List<ISentence>> Prove(
            List<Rule> clauses, IReadOnlyList<ISentence> goals,
            Dictionary<Variable, Term> theta, List<ISentence> assumed,
            int depth, Counter counter, HashSet<string> abducibles, int maxDepth)
        {
            if (goals.Count == 0)
            {
                yield return assumed;
                yield break;
            }
            if (depth > maxDepth) yield break;

            var goal = Bindings.Apply(goals[0], theta);
            var rest = goals.Skip(1).ToList();
            var sig = Bindings.Signature(goal);

            foreach (var clause in clauses)
            {
                var fresh = clause.Renamed(counter.Next++);
                if (Bindings.Signature(fresh.Head) != sig) continue;
                if (!Bindings.TryUnify(goal, fresh.Head, out var mgu)) continue;

                var subgoals = new List<ISentence>(fresh.Premises);
                subgoals.AddRange(rest);

                foreach (var solution in Prove(clauses, subgoals, Bindings.Extend(theta, mgu),
                             assumed, depth + 1, counter, abducibles, maxDepth))
                    yield return solution;
            }

            if (!abducibles.Contains(Bindings.SymbolOf(goal)) || !goal.IsGround()) yield break;
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
