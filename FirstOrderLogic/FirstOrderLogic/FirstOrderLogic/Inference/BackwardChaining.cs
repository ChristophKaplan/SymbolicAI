using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Goal-driven depth-first proof search with backtracking (cf. AIMA FOL-BC-ASK). The depth
    // bound guards cyclic rules, so a proof deeper than maxDepth reads as "not entailed".
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

        // Every proof of `goals`, yielded as the literals assumed along the way; a ground goal over
        // an abducible predicate may be assumed instead of proven.
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
            if (depth > maxDepth)
            {
                counter.DepthCutoff = true;
                yield break;
            }

            var goal = theta.Apply(goals[0]);
            var rest = goals.Skip(1).ToList();

            // Negation as failure: NAF l holds iff no instance of l is derivable (the same ∄
            // reading ForwardChaining uses for variables only occurring under NAF). The sub-proof
            // gets no abducibles and a throwaway assumption list — failure must be genuine.
            if (goal.IsNaf)
            {
                var target = goal.Children[0];
                var outerCutoff = counter.DepthCutoff;
                counter.DepthCutoff = false;
                var derivable = Prove(clauses, new List<ISentence> { target }, theta, new List<ISentence>(),
                    depth + 1, counter, NoAbducibles, maxDepth).Any();
                var truncated = counter.DepthCutoff;
                counter.DepthCutoff = outerCutoff || truncated;

                if (derivable) yield break;

                // A sub-proof cut off by the depth bound is unfinished, not failed: reading it
                // as failure would turn the bound's conservative "not entailed" into a wrong
                // positive answer, so NAF only succeeds on an exhaustive failure.
                if (truncated) yield break;

                foreach (var solution in Prove(clauses, rest, theta, assumed,
                             depth + 1, counter, abducibles, maxDepth))
                    yield return solution;
                yield break;
            }

            foreach (var clause in clauses)
            {
                var fresh = clause.Renamed(counter.Next++);
                if (!Unificator.TryMatch(goal, fresh.Head, out var match)) continue;

                // NAF premises go after the positive premises so their variables are bound by the
                // time the failure test runs.
                var subgoals = new List<ISentence>(fresh.Premises);
                subgoals.AddRange(fresh.NafPremises.Select(n =>
                    (ISentence)new ComplexSentence(Connective.LogicSymbol.NAF, n)));
                subgoals.AddRange(rest);

                foreach (var solution in Prove(clauses, subgoals, theta.Extend(match.Substitutions),
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
        // DepthCutoff records whether any branch was abandoned at maxDepth, so NAF can tell an
        // exhaustive failure from a truncated search.
        internal sealed class Counter
        {
            public int Next;
            public bool DepthCutoff;
        }
    }
}
