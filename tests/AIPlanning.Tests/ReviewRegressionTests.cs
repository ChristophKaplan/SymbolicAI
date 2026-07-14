using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    // Regression tests pinning the correctness bugs found (and fixed) in the July 2026
    // code review. Each test failed against the defect it describes.
    [TestFixture]
    [Category("Regression")]
    public class ReviewRegressionTests : PlanningTestBase {

        // Fixed defect: OperatorGraph.cs GetActionsForLiteral uses exact literal equality, so the
        // ground runtime literal Q(Obj) never matches the operator-graph node Q(x) created from a
        // non-ground chained precondition — the second action in the chain never fires.
        [Test]
        public void Issue01_ChainedNonGroundPreconditions_TwoStepChainIsSolved() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "R(Obj)" });

            var a1 = Factory.Create("A1", new() { "P(x)" }, new() { "Q(x)" });
            var a2 = Factory.Create("A2", new() { "Q(x)" }, new() { "R(x)" });

            var problem = new GpProblem(initialState, goals, new() { a1, a2 });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "P(Obj) -> A1 -> Q(Obj) -> A2 -> R(Obj) is a trivial 2-step plan; " +
                "an empty solution means A2's non-ground precondition node Q(x) was never " +
                "matched by the ground literal Q(Obj)");
        }

        // Fixed defect: GpPlanGraph.cs FindSolutions has no success base case at level 0, so goals
        // already satisfied in the initial state force a needless expansion and yield a spurious
        // one-step persist plan instead of a zero-step plan.
        [Test]
        public void Issue02_GoalsAlreadySatisfiedInInitialState_YieldZeroStepPlan() {
            var initialState = Factory.StringToSentence(new() {
                "Have(Cake)",
                "Subject(Subject1)"
            });
            var goals = Factory.StringToSentence(new() { "Have(Cake)" });
            var noOp = Factory.Create("NoOp", new() { "Subject(z)" }, new() { "Subject(z)" });

            var problem = new GpProblem(initialState, goals, new() { noOp });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "goals contained in the initial state are trivially achieved");

            var plan = solution.GetSolution(0);
            var realActions = plan.Values
                .SelectMany(set => set.GetActions(ignorePersistence: true))
                .ToList();
            Assert.That(realActions, Is.Empty,
                "no real (non-persist) action is needed when the goals already hold");
            Assert.That(plan, Is.Empty,
                "goals already satisfied at level 0 must yield a plan with ZERO action layers " +
                "(like the empty-goals case); a persist-only layer is a spurious expansion. " +
                $"Actual layers: [{string.Join(", ", plan.Keys)}]");
        }

        // Fixed defect: OperatorGraph reachability is driven purely by precondition edges
        // (MapPreConditionsToAction), so an action with an empty precondition list is never
        // returned by GetActionsForLiteral and can never fire.
        [Test]
        public void Issue03_ActionWithEmptyPreconditions_CanFire() {
            var initialState = Factory.StringToSentence(new() { "Unrelated(Thing)" });
            var goals = Factory.StringToSentence(new() { "R(Obj)" });

            var spawn = Factory.Create("Spawn", new List<string>(), new() { "R(Obj)" });

            var problem = new GpProblem(initialState, goals, new() { spawn });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Spawn has no preconditions and directly produces the goal R(Obj); " +
                "an empty solution means precondition-less actions are unreachable in the " +
                "operator graph");
        }

        // Fixed defect: GpBeliefState.GetSubSetOfNodesMatching (GpBeliefState.cs) applies
        // Distinct() and callers compare the count against literals.Count, so duplicate goal
        // literals make a trivially solvable problem unsolvable.
        [Test]
        public void Issue04_DuplicateGoalLiterals_ProblemStaysSolvable() {
            var initialState = Factory.StringToSentence(new() { "Subject(Subject1)" });
            var goals = Factory.StringToSentence(new() { "Have(Cake)", "Have(Cake)" });

            var bake = Factory.Create("Bake", new() { "Subject(z)" }, new() { "Have(Cake)" });

            var problem = new GpProblem(initialState, goals, new() { bake });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "the duplicated goal Have(Cake) is achievable in one Bake step; an empty " +
                "solution means Distinct() made the goal-count comparison fail forever");
        }

        // Fixed defect: NoGoods.IsStable (NoGoods.cs) compares the TOTAL nogood count across
        // expansions, but every failed extraction at a NEW level adds a nogood keyed by that
        // level, so the count grows strictly and the termination branch in GraphPlanAlgo.cs
        // never fires -> infinite loop on unsolvable problems whose goals stay pairwise non-mutex.
        [Test]
        public void Issue05_UnsolvableThreeWayConflict_TerminatesWithEmptySolution() {
            // One-shot resource Res(Token): each Make consumes it and produces two of the three
            // goods, so any PAIR of goals is achievable (goals stay pairwise non-mutex at
            // level-off, blocking the goalsReachable/level-off exit) but all three jointly are not.
            var initialState = Factory.StringToSentence(new() { "Res(Token)" });
            var goals = Factory.StringToSentence(new() {
                "Have(GoodA)", "Have(GoodB)", "Have(GoodC)"
            });

            var makeAb = Factory.Create("MakeAB",
                new() { "Res(Token)" },
                new() { "Have(GoodA)", "Have(GoodB)", "-Res(Token)" });
            var makeBc = Factory.Create("MakeBC",
                new() { "Res(Token)" },
                new() { "Have(GoodB)", "Have(GoodC)", "-Res(Token)" });
            var makeAc = Factory.Create("MakeAC",
                new() { "Res(Token)" },
                new() { "Have(GoodA)", "Have(GoodC)", "-Res(Token)" });

            var problem = new GpProblem(initialState, goals, new() { makeAb, makeBc, makeAc });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.True,
                "only two of the three goods can ever be produced — the planner must " +
                "recognise the problem as unsolvable and return an empty solution");
        }

        // Fixed defect: GpAction.SpecifyAction (GpAction.cs) applies Unificator.Apply, which
        // substitutes sequentially without resolving triangular substitutions like
        // {x->Obj, z->f(x)}, leaving under-instantiated (non-ground) literals in the action.
        [Test]
        public void Issue06_TriangularSubstitution_FullyGroundsAction() {
            var action = Factory.Create("Ground", new() { "P(x, z)" }, new() { "Q(z)" });

            // Unifying P(x, z) with P(Obj, f(x)) yields the triangular substitution
            // {x -> Obj, z -> f(x)}; its correct resolved form is {x -> Obj, z -> f(Obj)}.
            var pattern = (ISentence)Factory.StringToSentence(new() { "P(x, z)" })[0];
            var target = (ISentence)Factory.StringToSentence(new() { "P(Obj, f(x))" })[0];
            var unificator = new Unificator(pattern, target);
            Assert.That(unificator.IsUnifiable, Is.True, "sanity: the two literals must unify");

            action.SpecifyAction(unificator);

            var nonGround = action.Preconditions.Concat(action.Effects)
                .Where(literal => !literal.IsGround())
                .ToList();
            Assert.That(nonGround, Is.Empty,
                "after grounding with {x->Obj, z->f(x)} every literal must be fully ground " +
                "(z must resolve to f(Obj)); non-ground literals reaching the planner: " +
                $"[{string.Join(", ", nonGround)}]");
        }
    }
}
