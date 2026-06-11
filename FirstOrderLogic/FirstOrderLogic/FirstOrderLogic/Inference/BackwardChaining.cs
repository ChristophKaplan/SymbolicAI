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
        private readonly int _maxDepth;

        public BackwardChaining(int maxDepth = 500)
        {
            _maxDepth = maxDepth;
        }

        public bool Entails(IEnumerable<ISentence> kb, ISentence query)
        {
            if (!query.IsLiteral) return false;
            var clauses = kb.Select(Rule.From).Where(c => c != null).Select(c => c!).ToList();
            var goals = new List<ISentence> { query };
            return Prove(clauses, goals, new Dictionary<Variable, Term>(), 0, new Counter()).Any();
        }

        // Every answer substitution for `goals` (a conjunction proved left-to-right).
        private IEnumerable<Dictionary<Variable, Term>> Prove(
            List<Rule> clauses, IReadOnlyList<ISentence> goals,
            Dictionary<Variable, Term> theta, int depth, Counter counter)
        {
            if (goals.Count == 0)
            {
                yield return theta;
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

                foreach (var solution in Prove(clauses, subgoals, theta2, depth + 1, counter))
                    yield return solution;
            }
        }

        // A holder rather than a ref int because iterator methods cannot take by-reference parameters.
        private sealed class Counter
        {
            public int Next;
        }
    }
}
