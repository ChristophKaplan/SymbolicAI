# SymbolicAI

A symbolic-AI toolkit in C#, combining first-order logic inference and classical
(GraphPlan) planning.

| Project | Description |
|---|---|
| `FirstOrderLogic/` | FOL representation, parsing, equivalence transformations, normal forms, and a resolution theorem prover. |
| `AIPlanning/` | GraphPlan classical planner with FOL action schemas, built on FirstOrderLogic. |

## Layout

```
SymbolicAI/
  FirstOrderLogic/   FOL library + example, tests, perf bench
  AIPlanning/        GraphPlan library + example, tests
  SymbolicAI.sln     unified solution
```

## Dependencies

Both projects reference two shared libraries that live in **sibling repos** (not part
of this repo). Clone them next to `SymbolicAI/` so the relative project references
resolve:

```
Development/
  SymbolicAI/   (this repo)
  LRParser/     parser used by FirstOrderLogic
  Logger/       logging
```

## Building

```
dotnet build SymbolicAI.sln
```

Targets `netstandard2.1` / `net8.0`, C# 9. The `AIPlanning` library stays
`netstandard2.1` so it can be dropped into Unity as a managed plugin.

---

# FirstOrderLogic

A library for parsing and reasoning over first-order logic: a parser, an AST,
equivalence transformations, normal forms (PNF/CNF/Skolem), a resolution theorem
prover with unification, kernel sets for explainability, and a WalkSAT solver for the
propositional fragment.

## Features

- **Parser** with readable ASCII syntax and operator aliases.
- **Transformations**: simplification, implication/biconditional elimination, De Morgan,
  quantifier pulling, distribution.
- **Normal forms**: Prenex (PNF), Conjunctive (CNF), Skolem.
- **Resolution** theorem prover with unification (optional clause subsumption).
- **Kernel sets**: minimal entailing subsets of a belief base (Hansson 1994).
- **WalkSAT** for propositional clause sets.
- **Time-indexed predicates** for temporal reasoning (`P(x)^1`).

## Syntax

- Conjunction — `AND`, `&&`
- Disjunction — `OR`, `||`
- Implication — `IMPLIES`, `=>`
- Biconditional — `IFF`, `<=>`
- Negation — `NOT`, `!`, `-`, `~`, `¬`
- Quantifiers — `FORALL x ...`, `EXISTS x ...`
- Constants — `TRUE`, `FALSE`
- Time index — `P(x)^1`

Identifiers starting with `x`, `y`, `z`, `w` are variables; all others are constants.

## Example

```csharp
using FirstOrderLogic;

var logic = new FirstOrderLogic.FirstOrderLogic();

var kb = (ISentence)logic.TryParse("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))");
var skolem = logic.SkolemForm(logic.ToPrenexForm(kb, out _));
var goal = (ISentence)logic.TryParse("Mortal(Sokrates)");

bool entailed = Resolution.Resolve(skolem, goal); // True
```

`Resolution.Resolve(kb, goal, useSubsumption: true)` enables clause subsumption (helps on
large, redundant problems; off by default). `Resolution.Resolve(kb, goal, maxRounds: n)` bounds the
saturation loop — FOL entailment is only semi-decidable, so a non-entailed KB with
function symbols can otherwise grow clauses forever; exceeding the budget throws
`InvalidOperationException`. A runnable version lives in
`FirstOrderLogic/FirstOrderLogicExample/Program.cs`.

## Kernel sets

A **kernel** is a minimal subset of a belief base `K` that entails `α`; multiple kernels
expose independent proof paths. Entailment delegates to `Resolution`.

```csharp
var ks = new KernelSets();
var K = new List<ISentence> {
    (ISentence)logic.TryParse("P(a)"),
    (ISentence)logic.TryParse("P(x) => Q(x)"),
    (ISentence)logic.TryParse("R(b)"),   // irrelevant to Q(a)
};
var α = (ISentence)logic.TryParse("Q(a)");

var kernel = ks.FindKernel(K, α);        // { P(a), P(x) => Q(x) }  (null if K does not entail α)
var all    = ks.FindAllKernels(K, α);    // every independent proof path
```

---

# AIPlanning

A C# implementation of the **GraphPlan** algorithm (Blum & Furst, 1995) for classical
AI planning with first-order action schemas.

## What it does

Given FOL literals describing an initial world state, goal literals, and action schemas
with preconditions and effects, the planner produces a layered action plan that
transforms the initial state into one satisfying the goals — or reports that no plan
exists.

```csharp
using AIPlanning.Planning.GraphPlan;

var factory = new GpActionFactory();

var initialState = factory.StringToSentence(new() {
    "At(Subject1, MyLocation)",
    "-At(Subject1, Supermarket)",
    "-At(Subject1, Home)",
    "-Have(Cake)",
    "Food(Cake)",
    "Subject(Subject1)"
});

var goals = factory.StringToSentence(new() {
    "Have(Cake)",
    "At(Subject1, Home)"
});

var move = factory.Create("Move",
    preconditions: new() { "-At(z, x)", "At(z, y)", "Subject(z)" },
    effects:       new() { "At(z, x)", "-At(z, y)" });

var work = factory.Create("Work",
    preconditions: new() { "At(z, Work)", "Subject(z)" },
    effects:       new() { "Have(Money)" });

var buyFood = factory.Create("BuyFood",
    preconditions: new() { "At(z, Supermarket)", "Have(Money)", "Food(x)", "Subject(z)" },
    effects:       new() { "Have(x)", "-Have(Money)" });

var problem  = new GpProblem(initialState, goals, new() { move, work, buyFood });
var solution = problem.Solve();

Console.WriteLine(solution);
```

A runnable version lives in `AIPlanning/AIPlanningExample/Program.cs`.

## Features

- **Lifted action schemas** — actions written with logical variables, grounded against
  the current state at plan time.
- **Mutex reasoning** — standard GraphPlan mutual-exclusion analysis on literals and actions.
- **NoGoods memoisation** — failed subgoal sets cached across levels, with the Blum/Furst
  termination criterion (level-off + stable nogoods => no plan).
- **Persistence (no-op) actions** — automatically inserted so literals carry forward across layers.
- **Multi-agent / joint planning** — multiple agent constants in one `GpProblem` produce
  per-agent grounded instances solvable in one pass (see
  `AIPlanning/AIPlanning/AIPlanningTests/MultiAgentPlanningTests.cs`; works for small N,
  scales super-linearly).
- **`GpSolution`** — distinguishes "no plan exists" from a trivially-satisfied zero-step plan.

## Action syntax

| Form                | Meaning                                          |
|---------------------|--------------------------------------------------|
| `At(Bob, Home)`     | positive ground literal                          |
| `-Have(Cake)`       | negated literal (closed-world)                   |
| `At(z, y)`          | literal with logical variables (lower-case head) |
| `Subject(Subject1)` | typing predicate used to bind variables to terms |

Effects can be positive (add) or negative (delete). Variables shared between
preconditions and effects are unified at grounding time against constants in the world state.

## Further reading

The papers in `AIPlanning/` are the working references:

- `graphplan_paper.pdf` — Blum & Furst, *Fast Planning Through Planning Graph Analysis*
- `operatorgraph.pdf` — notes on the operator-grounding structure
- `mea_graphplan.pdf` — means-ends analysis sketches

## License

Copyright © Christoph Kaplan.
