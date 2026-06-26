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

A C# implementation of the **GraphPlan** algorithm (Blum & Furst, 1995/1997) for classical
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

- **Lifted action schemas** — actions written with logical variables, grounded once via the
  operator graph (the goal-relevant instantiations), with applicability re-checked against
  each layer's state.
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

## How the pieces fit together

This implementation combines two of the papers below along the line they themselves draw
between *lifted* (variables kept) and *grounded* (variables instantiated) reasoning:

- **`OperatorGraph`** (Smith & Peot) is **lifted**: it back-chains from the goal through the
  action schemas, unifying effects with preconditions, to isolate the operators relevant to
  the goal and the instantiations they require. It is built once per problem and acts as a
  relevance filter / grounder.
- **The planning graph** (`GpPlanGraph` / `GpLayer`, Blum & Furst) is **grounded**: every
  action node is a fully instantiated operator, and mutex reasoning (interference + competing
  needs) runs over ground literals and actions, level by level.

Using a lifted operator graph as a relevance filter in front of a grounded planning graph is
exactly the goal-directed scheme described in mea_graphplan §4.1. The grounding cost, and the
O(groundings²) mutex check per layer, are inherent to grounded GraphPlan — which is why the
multi-agent / joint-planning encoding scales super-linearly with the number of agents.

## Further reading

The papers in `AIPlanning/` are the working references:

- `graphplan_paper.pdf` — A. Blum & M. Furst, *Fast Planning Through Planning Graph Analysis*
  (IJCAI-95; extended version in *Artificial Intelligence* 90(1–2):281–300, 1997). The core
  GraphPlan algorithm: planning-graph construction, mutex propagation, level-by-level backward
  search, and the "level-off" termination test.
- `operatorgraph.pdf` — D. Smith & M. Peot, *Postponing Threats in Partial-Order Planning*
  (AAAI-93). Defines the **operator graph** — a goal-back-chained, unification-based (lifted)
  structure capturing which operators are relevant to a goal. Source of the `OperatorGraph`
  used here (Start/Finish operators, use counts, threats as effects unifying with the negation
  of a precondition).
- `mea_graphplan.pdf` — S. Kambhampati, E. Parker & E. Lambrecht, *Understanding and Extending
  Graphplan*. Reconstructs GraphPlan as forward state-space refinement over disjunctive plans
  (with backward search as a dynamic CSP), and in §4.1 proposes using operator graphs to make
  GraphPlan goal-directed — the way the operator graph and planning graph fit together here.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and [NOTICE](NOTICE).

Copyright © 2026 Christoph Kaplan.
