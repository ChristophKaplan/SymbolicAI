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
            if (!query.IsLiteral)
            {
                return false;
            }

            return Prove(Rule.FromAll(kb), query, NoAbducibles, _maxDepth).Any();
        }

        // Entry point supplying the boilerplate of a fresh top-level proof. The constant
        // universe (KB + query) feeds the per-instance NAF fallback, mirroring ForwardChaining.
        internal static IEnumerable<List<ISentence>> Prove(
            List<Rule> clauses, ISentence goal, HashSet<string> abducibles, int maxDepth)
        {
            var constants = clauses
                .SelectMany(c => c.Premises.Concat(c.NafPremises).Append(c.Head))
                .Append(goal)
                .SelectMany(ForwardChaining.ConstantsOf)
                .Distinct()
                .ToList();
            return Prove(clauses, GoalList.Of(goal), Substitution.Empty,
                new List<ISentence>(), new List<ISentence>(), 0, new Counter(), abducibles, maxDepth,
                constants, new HashSet<Variable>());
        }

        // Every proof of `goals`, yielded as the literals assumed along the way; a ground goal over
        // an abducible predicate may be assumed instead of proven. `denied` carries the NAF targets
        // the path has committed to: assumptions and NAF failures must not defeat each other,
        // whichever comes first, or the "proof" does not derive its goal.
        internal static IEnumerable<List<ISentence>> Prove(
            List<Rule> clauses, GoalList? goals,
            Substitution theta, List<ISentence> assumed, List<ISentence> denied,
            int depth, Counter counter, HashSet<string> abducibles, int maxDepth,
            IReadOnlyList<Term> constants, HashSet<Variable> nafOnly)
        {
            if (goals is null)
            {
                yield return assumed;
                yield break;
            }
            if (depth > maxDepth)
            {
                counter.DepthCutoff = true;
                yield break;
            }

            var goal = theta.Apply(goals.Head);
            var rest = goals.Tail;

            // Negation as failure: NAF l holds iff no instance of l is derivable (the same ∄
            // reading ForwardChaining uses for variables only occurring under NAF). The sub-proof
            // gets no abducibles and a throwaway assumption list — failure must be genuine.
            if (goal.IsNaf)
            {
                var target = goal.Children[0];

                // An assumption already made on this path is derivable by fiat, so it defeats
                // the failure test even though the KB-only sub-proof below cannot see it.
                var blocked = assumed.Any(a => Unificator.TryMatch(target, a, out _));
                if (!blocked)
                {
                    var (derivable, truncated) = TestNaf(target, theta);
                    // A sub-proof cut off by the depth bound is unfinished, not failed: reading
                    // it as failure would turn the bound's conservative "not entailed" into a
                    // wrong positive answer, so NAF only succeeds on an exhaustive failure.
                    if (!derivable && !truncated)
                    {
                        foreach (var solution in Prove(clauses, rest, theta, assumed,
                                     new List<ISentence>(denied) { target },
                                     depth + 1, counter, abducibles, maxDepth, constants, nafOnly))
                        {
                            yield return solution;
                        }

                        yield break;
                    }
                }

                // A defeated instance only defeats itself when the NAF variable is shared with
                // the rest of the proof (bound to a universal fact's variable or still a free
                // query variable): the other ground instances stay entailed, so re-test per
                // instance over the constant universe — ForwardChaining's Finding-22 semantics.
                // Variables occurring only under NAF keep the ∄ reading and are never grounded.
                var groundable = target.VariablesOf().Where(v => !nafOnly.Contains(v)).ToList();
                if (groundable.Count == 0)
                {
                    yield break;
                }

                foreach (var grounding in Groundings(groundable, constants))
                {
                    var extendedTheta = theta.Extend(grounding);
                    var grounded = extendedTheta.Apply(target);
                    if (assumed.Any(a => Unificator.TryMatch(grounded, a, out _)))
                    {
                        continue;
                    }

                    var (derivable, truncated) = TestNaf(grounded, extendedTheta);
                    if (derivable || truncated)
                    {
                        continue;
                    }

                    foreach (var solution in Prove(clauses, rest, extendedTheta, assumed,
                                 new List<ISentence>(denied) { grounded },
                                 depth + 1, counter, abducibles, maxDepth, constants, nafOnly))
                    {
                        yield return solution;
                    }
                }
                yield break;

                (bool derivable, bool truncated) TestNaf(ISentence t, Substitution th)
                {
                    var outerCutoff = counter.DepthCutoff;
                    counter.DepthCutoff = false;
                    var derivable = Prove(clauses, GoalList.Of(t), th,
                        new List<ISentence>(), new List<ISentence>(),
                        depth + 1, counter, NoAbducibles, maxDepth, constants, nafOnly).Any();
                    var truncated = counter.DepthCutoff;
                    counter.DepthCutoff = outerCutoff || truncated;
                    return (derivable, truncated);
                }
            }

            foreach (var clause in clauses)
            {
                var fresh = clause.Renamed(counter.Next++);
                if (!Unificator.TryMatch(goal, fresh.Head, out var match))
                {
                    continue;
                }

                // NAF premises go after the positive premises so their variables are bound by the
                // time the failure test runs.
                var subgoals = new List<ISentence>(fresh.Premises);
                subgoals.AddRange(fresh.NafPremises.Select(n =>
                    (ISentence)new ComplexSentence(Connective.LogicSymbol.NAF, n)));
                var pending = GoalList.Push(subgoals, rest);

                if (fresh.NafPremises.Count > 0)
                {
                    var positive = fresh.Premises.SelectMany(p => p.VariablesOf()).ToHashSet();
                    foreach (var variable in fresh.NafPremises.SelectMany(n => n.VariablesOf()))
                    {
                        if (!positive.Contains(variable))
                        {
                            nafOnly.Add(variable);
                        }
                    }
                }

                foreach (var solution in Prove(clauses, pending, theta.Extend(match.Substitutions),
                             assumed, denied, depth + 1, counter, abducibles, maxDepth, constants, nafOnly))
                {
                    yield return solution;
                }
            }

            if (!abducibles.Contains(goal.SymbolOf()) || !goal.IsGround())
            {
                yield break;
            }

            if (assumed.Any(a => a.IsNegationOf(goal)))
            {
                yield break;
            }
            // Assuming an instance of a literal an earlier NAF failed on would retroactively
            // defeat that failure test.
            if (denied.Any(d => Unificator.TryMatch(d, goal, out _)))
            {
                yield break;
            }

            var extended = assumed.Any(a => a.Equals(goal))
                ? assumed
                : new List<ISentence>(assumed) { goal };

            foreach (var solution in Prove(clauses, rest, theta, extended, denied,
                         depth + 1, counter, abducibles, maxDepth, constants, nafOnly))
            {
                yield return solution;
            }
        }

        private static IEnumerable<Dictionary<Variable, Term>> Groundings(
            IReadOnlyList<Variable> variables, IReadOnlyList<Term> constants)
        {
            if (variables.Count == 0)
            {
                yield return new Dictionary<Variable, Term>();
                yield break;
            }

            var rest = variables.Skip(1).ToList();
            foreach (var constant in constants)
            {
                foreach (var grounding in Groundings(rest, constants))
                {
                    grounding[variables[0]] = constant;
                    yield return grounding;
                }
            }
        }

        // An immutable cons-list (null is empty). Expanding a rule replaces the current goal with the
        // rule's premises and keeps the rest, so sharing the tail makes that cost the premise count
        // rather than a copy of the whole remaining goal list at every step of every proof branch.
        internal sealed class GoalList
        {
            internal readonly ISentence Head;
            internal readonly GoalList? Tail;

            private GoalList(ISentence head, GoalList? tail)
            {
                Head = head;
                Tail = tail;
            }

            internal static GoalList Of(ISentence goal)
            {
                return new GoalList(goal, null);
            }

            internal static GoalList? Push(IReadOnlyList<ISentence> goals, GoalList? tail)
            {
                var result = tail;
                for (var i = goals.Count - 1; i >= 0; i--)
                {
                    result = new GoalList(goals[i], result);
                }
                return result;
            }
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
