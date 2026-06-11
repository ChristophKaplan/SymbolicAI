using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Backward chaining (goal-driven) over definite clauses: to prove a goal, unify it with the head of
    // each clause and recursively prove that clause's premises, left-to-right and depth-first with
    // backtracking — the proof strategy behind Prolog. Sound; complete for function-free clause sets
    // within the depth bound. Like Prolog it can recurse without end on left-recursive/cyclic rules, so
    // a depth limit guards against runaway recursion (exceeding it prunes that branch rather than
    // reporting "not entailed"). Only the definite-clause subset of the KB participates. Cf. AIMA
    // FOL-BC-ASK.
    public class BackwardChaining
    {
        private readonly int _maxDepth;

        public BackwardChaining(int maxDepth = 500)
        {
            _maxDepth = maxDepth;
        }

        // True iff the definite-clause subset of `kb` entails `query` (a positive literal).
        public bool Entails(IEnumerable<ISentence> kb, ISentence query)
        {
            if (!query.IsLiteral || query.IsNegation) return false;
            var clauses = kb.Select(DefiniteClause.From).Where(c => c != null).Select(c => c!).ToList();
            var goals = new List<ISentence> { query };
            return Prove(clauses, goals, new Dictionary<Variable, Term>(), 0, new Counter()).Any();
        }

        // Every answer substitution for `goals` (a conjunction proved left-to-right) under the bindings
        // accumulated in `theta`. The head of each clause is standardized apart on use via `counter`.
        private IEnumerable<Dictionary<Variable, Term>> Prove(
            List<DefiniteClause> clauses, IReadOnlyList<ISentence> goals,
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

        // Monotonic source of standardize-apart suffixes. A holder (not a ref int) because iterator
        // methods cannot take by-reference parameters.
        private sealed class Counter
        {
            public int Next;
        }
    }
}
