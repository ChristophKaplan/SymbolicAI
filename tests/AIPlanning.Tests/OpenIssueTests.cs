using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;
using NUnit.Framework;

namespace AIPlanningTests {
    // Repro tests for the findings of the July 2026 whole-project reviews, all fixed since.
    // Each test asserts the CORRECT behavior and now serves as a regression pin.
    [TestFixture]
    [Category("OpenIssue")]
    public class OpenIssueTests : PlanningTestBase {

        // Finding 2 — IsEffectsApplicable adds the effect-vs-precondition binding to the
        // PRODUCER's unificator set, so a consumer variable that occurs only in preconditions
        // and is instantiable only via another action's effect is never bound; every consumer
        // instance is dropped as non-ground and the planner reports "no plan".
        [Test]
        public void Finding02_PreconditionOnlyVariable_BoundViaProducerEffect_IsSolvable() {
            var initialState = Factory.StringToSentence(new() { "Subject(Bob)" });
            var goals = Factory.StringToSentence(new() { "Fed(Bob)" });

            var bake = Factory.Create("Bake", new() { "Subject(z)" }, new() { "Have(Bread)" });
            var eat = Factory.Create("Eat", new() { "Have(x)", "Subject(z)" }, new() { "Fed(z)" });

            var problem = new GpProblem(initialState, goals, new() { bake, eat });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Bake produces Have(Bread), then Eat with x=Bread yields Fed(Bob) — a valid " +
                "2-step plan; an empty solution means Eat's precondition-only variable x was " +
                "never bound from the producer's effect");
        }

        // Finding 5 — IsApplicableToPreconditions compares the Distinct()-ed matched-node
        // count against the raw Preconditions.Count, so a duplicated precondition literal
        // makes the action permanently inapplicable (mirror of the fixed duplicate-goals bug).
        [Test]
        public void Finding05_DuplicatePreconditionLiterals_ActionStillFires() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)" });

            var act = Factory.Create("Act", new() { "P(Obj)", "P(Obj)" }, new() { "G(Obj)" });

            var problem = new GpProblem(initialState, goals, new() { act });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "P(Obj) holds, so Act is applicable regardless of the duplicated precondition");
        }

        // Finding 5 (grounding-collapse variant) — the duplicate arises without user-written
        // duplicates when two distinct precondition variables ground to the same literal.
        [Test]
        public void Finding05_PreconditionsCollapsingUnderGrounding_ActionStillFires() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)" });

            var act = Factory.Create("Act", new() { "P(x)", "P(y)" }, new() { "G(x)" });

            var problem = new GpProblem(initialState, goals, new() { act });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "with Obj as the only object, x=y=Obj grounds Act to the duplicated " +
                "precondition {P(Obj), P(Obj)}, which must still count as satisfied");
        }

        // Finding 6 — InstantiateActions keys a dictionary by GpAction, whose equality is
        // content-based, so two content-equal actions in the problem's action list crash
        // Solve with ArgumentException instead of being deduplicated.
        [Test]
        public void Finding06_ContentEqualDuplicateActions_AreTolerated() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });
            var act = Factory.Create("Act", new() { "P(K)" }, new() { "G(K)" });
            var duplicate = Factory.Create("Act", new() { "P(K)" }, new() { "G(K)" });

            var problem = new GpProblem(initialState, goals, new() { act, duplicate });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "a duplicated action definition changes nothing semantically and must not " +
                "crash grounding or lose the one-step plan");
        }

        // Finding 7 — FindSolutions treats an empty belief state at levelIndex > 0 as failure
        // (its success arm is gated on the unreachable levelIndex < 0), so a trivially
        // satisfied empty goal set extracts as "no plan exists".
        [Test]
        public void Finding07_EmptyGoals_AtHigherLevel_AreTriviallySatisfied() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new());
            var act = Factory.Create("X", new() { "P(K)" }, new() { "Q(K)" });
            var problem = new GpProblem(initialState, goals, new() { act });

            var graph = new GpPlanGraph(problem);
            graph.ExpandGraph();
            var solution = graph.ExtractSolution(1, new NoGoods());

            Assert.That(solution.IsEmpty, Is.False,
                "an empty goal set is satisfied by the empty plan at every level; IsEmpty " +
                "must mean 'no plan exists', not 'nothing was requested'");
        }

        // Finding 8 — GpBeliefState.Equals (and GpActionSet.Equals alike) uses equal counts
        // plus one-directional Contains, which is multiset-unsafe: {P, P} equals {P, Q}. The
        // IEnumerable constructor accepts duplicates, and NoGoods keys hash sets on this
        // equality — the same defect that was already fixed on GpAction.
        [Test]
        public void Finding08_BeliefStateEquality_IsMultisetSafe() {
            var p = new GpLiteralNode(L("P(Obj)"));
            var q = new GpLiteralNode(L("Q(Obj)"));

            var pp = new GpBeliefState(new List<GpLiteralNode> { p, p });
            var pq = new GpBeliefState(new List<GpLiteralNode> { p, q });

            Assert.That(pp.Equals(pq), Is.False,
                "{P, P} and {P, Q} are different states even though the counts match and " +
                "every element of the first occurs in the second");
        }

        // Finding 9 — MapPreConditionsToAction skips creating a literal node for a non-ground
        // precondition whenever ANY existing node unifies with it, even a strictly more
        // specific one; the general precondition then loses every grounding not covered by
        // that node, and solvability depends on action declaration order.
        [Test]
        public void Finding09_GeneralPrecondition_AfterSpecificOne_KeepsItsOwnGroundings() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var specific = Factory.Create("ASpec", new() { "Q(Home)" }, new() { "G(K)" });
            var general = Factory.Create("AGen", new() { "Q(y)" }, new() { "G(K)" });
            var maker = Factory.Create("Maker", new() { "P(K)" }, new() { "Q(TreeA)" });

            var problem = new GpProblem(initialState, goals, new() { specific, general, maker });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Maker then AGen(y=TreeA) is a valid 2-step plan; an empty solution means " +
                "AGen's precondition Q(y) was anchored onto the more specific existing node " +
                "Q(Home) and the binding y=TreeA was never collected");
        }

        [Test]
        public void Finding09_ControlCase_GeneralDeclaredFirst_IsSolvable() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var general = Factory.Create("AGen", new() { "Q(y)" }, new() { "G(K)" });
            var specific = Factory.Create("ASpec", new() { "Q(Home)" }, new() { "G(K)" });
            var maker = Factory.Create("Maker", new() { "P(K)" }, new() { "Q(TreeA)" });

            var problem = new GpProblem(initialState, goals, new() { general, specific, maker });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "control: the same problem with AGen declared before ASpec — declaration " +
                "order must not change solvability");
        }

        // Finding 10 — when a producer's non-ground effect Q(w) unifies with a consumer's
        // non-ground anchor node Q(y), the unifier is variable-to-variable; the consumer's
        // instantiation combos substitute a variable and every instance is dropped as
        // non-ground. Ground producer instances' effects were never re-matched against the
        // anchor, so the constant binding y→K was never learned.
        [Test]
        public void Finding10_VariableToVariableProducerBinding_ResolvedViaGroundInstances() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var maker = Factory.Create("Maker", new() { "P(w)" }, new() { "Q(w)" });
            var user = Factory.Create("User", new() { "Q(y)" }, new() { "G(K)" });

            var problem = new GpProblem(initialState, goals, new() { maker, user });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "Maker grounds to P(K)->Q(K), so User with y=K yields G(K) — a valid 2-step " +
                "plan; an empty solution means the ground effect Q(K) was never re-matched " +
                "against User's anchor Q(y)");
            AssertPlanIsValid(problem, solution);
        }

        // Finding 10 (chained variant) — each fixpoint round unlocks the next link: only the
        // ground M1 instance reveals v=K for M2, and only the ground M2 instance reveals y=K.
        [Test]
        public void Finding10_ChainedVariableToVariableBindings_ResolvedAcrossTwoLinks() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var m1 = Factory.Create("M1", new() { "P(w)" }, new() { "Q(w)" });
            var m2 = Factory.Create("M2", new() { "Q(z)" }, new() { "R(z)" });
            var user = Factory.Create("User", new() { "R(y)" }, new() { "G(K)" });

            var problem = new GpProblem(initialState, goals, new() { m1, m2, user });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "M1;M2;User is a valid 3-step plan reachable only if grounding iterates to a " +
                "fixpoint across the whole producer chain");
            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void Finding10_ControlCase_GroundProducerEffect_StillSolvable() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var maker = Factory.Create("Maker", new() { "P(w)" }, new() { "Q(K)" });
            var user = Factory.Create("User", new() { "Q(y)" }, new() { "G(K)" });

            var problem = new GpProblem(initialState, goals, new() { maker, user });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "control: with a ground producer effect the binding y=K is learned during " +
                "construction and must keep working");
            AssertPlanIsValid(problem, solution);
        }

        [Test]
        public void Finding10_ControlCase_ConsumerVariableFlowsIntoEffect_StillSolvable() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "G(K)" });

            var maker = Factory.Create("Maker", new() { "P(w)" }, new() { "Q(w)" });
            var user = Factory.Create("User", new() { "Q(y)" }, new() { "G(y)" });

            var problem = new GpProblem(initialState, goals, new() { maker, user });
            var solution = SolveWithGuard(problem);

            Assert.That(solution.IsEmpty, Is.False,
                "control: y also occurs in User's effect, so the goal literal G(K) binds it " +
                "as a producer binding — this path must keep working");
            AssertPlanIsValid(problem, solution);
        }

        // Finding 11 — action insertion only checked that the precondition literals are PRESENT
        // in the previous literal layer, not that they are pairwise non-mutex there (Blum & Furst
        // require both); mutex-supported actions caused wasted extraction work.
        [Test]
        public void Finding11_ActionWithMutexPreconditions_IsNotInsertedIntoLayer() {
            var layer = new GpLayer(0);
            var p = layer.BeliefState.Add(new GpLiteralNode(L("P(Obj)")));
            var q = layer.BeliefState.Add(new GpLiteralNode(L("Q(Obj)")));
            p.TryAddMutexRelations(q, MutexType.InconsistentSupport);

            var needsBoth = new GpAction("NeedsBoth",
                new() { L("P(Obj)"), L("Q(Obj)") },
                new() { L("G(Obj)") });
            layer.ExpandActions(new List<GpAction> { needsBoth }, new Dictionary<ISentence, GpAction>());

            Assert.That(layer.ActionSet.GetActions(ignorePersistence: true), Is.Empty,
                "P(Obj) and Q(Obj) are mutex in this layer, so an action needing both " +
                "cannot fire here and must not enter the action layer");
        }

        // Finding 12 — the no-unificator instantiation path skipped the IsConsistent() filter
        // that the unified path applies, letting a directly-ground action with
        // self-contradictory effects into the operator graph.
        [Test]
        public void Finding12_GroundActionWithContradictoryEffects_IsFilteredFromGraph() {
            var initialState = Factory.StringToSentence(new() { "P(K)" });
            var goals = Factory.StringToSentence(new() { "Q(K)" });
            var broken = Factory.Create("Broken", new() { "P(K)" }, new() { "Q(K)", "-Q(K)" });

            var graph = new OperatorGraph(new GpProblem(initialState, goals, new() { broken }));

            var actions = graph.GetActionsForLiteral(L("P(K)"));
            Assert.That(actions.Select(a => a.Signifier), Does.Not.Contain("Broken"),
                "an action whose effects contain Q(K) and -Q(K) is inconsistent and must be " +
                "filtered on the direct (no-unificator) instantiation path too");
        }

        // Finding 13 — a non-ground goal like Have(x) silently yielded "no plan"; the design is
        // ground-literals-only, so the GpProblem boundary must reject it loudly.
        [Test]
        public void Finding13_NonGroundGoal_ThrowsAtProblemBoundary() {
            var initialState = Factory.StringToSentence(new() { "Have(Cake)" });
            var goals = Factory.StringToSentence(new() { "Have(x)" });

            Assert.That(() => new GpProblem(initialState, goals, new List<GpAction>()),
                Throws.ArgumentException,
                "GraphPlan states are ground-literal sets; a non-ground goal can never match " +
                "and must fail loudly instead of yielding an empty solution");
        }

        [Test]
        public void Finding13_NonGroundInitialStateLiteral_ThrowsAtProblemBoundary() {
            var initialState = Factory.StringToSentence(new() { "Have(x)" });
            var goals = Factory.StringToSentence(new() { "Have(Cake)" });

            Assert.That(() => new GpProblem(initialState, goals, new List<GpAction>()),
                Throws.ArgumentException,
                "a non-ground initial-state literal is equally unrepresentable and must be " +
                "rejected at the boundary");
        }

        // Finding 14 — SpecifyAction mutated the instance and recomputed its hash while GpAction
        // is used as a Dictionary/HashSet key; it must return a new instance and leave the
        // original (and its hash) untouched.
        [Test]
        public void Finding14_SpecifyAction_ReturnsNewInstance_AndLeavesOriginalUntouched() {
            var action = Factory.Create("Act", new() { "P(x)" }, new() { "Q(x)" });
            var originalHash = action.GetHashCode();

            var unificator = new Unificator(L("P(x)"), L("P(K)"));
            Assert.That(unificator.IsUnifiable, Is.True, "sanity: P(x) and P(K) must unify");

            var grounded = action.SpecifyAction(unificator);

            Assert.That(grounded, Is.Not.SameAs(action),
                "specifying must produce a new instance, not mutate a potential hash-set key");
            Assert.That(grounded.IsGround(), Is.True);
            Assert.That(action.GetHashCode(), Is.EqualTo(originalHash),
                "the original action's hash must not change");
            Assert.That(action.Preconditions.Single().IsGround(), Is.False,
                "the original action's literals must stay non-ground");
        }
    }
}
