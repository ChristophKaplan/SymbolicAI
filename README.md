# SymbolicAI

A symbolic-AI toolkit in C#, combining first-order logic inference and classical
(GraphPlan) planning.

| Project | Description |
|---|---|
| `src/FirstOrderLogic/` | FOL representation, parsing, equivalence transformations, normal forms, and a resolution theorem prover. |
| `src/AIPlanning/` | GraphPlan classical planner with FOL action schemas, built on FirstOrderLogic. |

## Layout

```
SymbolicAI/
  src/           FirstOrderLogic and AIPlanning libraries
  tests/         NUnit test projects
  examples/      runnable example programs
  benchmarks/    PerfBench
  docs/          design notes + reference papers
  SymbolicAI.sln unified solution
```

## Dependencies

Two shared libraries live in **sibling repos** (not part of this repo). Clone them next
to `SymbolicAI/` so the relative project references resolve:

```
Development/
  SymbolicAI/   (this repo)
  LRParser/     parser — referenced by FirstOrderLogic; AIPlanning gets it transitively
  Logger/       logging — used by LRParser, so both libraries pull it in transitively
```

## Building

```
dotnet build SymbolicAI.sln
```

C# 10. The `FirstOrderLogic` library targets `netstandard2.1` only; `AIPlanning`
multi-targets `netstandard2.1;net8.0`, so its `netstandard2.1` build can be dropped into
Unity as a managed plugin. Tests, examples, and benchmarks target `net8.0`.

---

# FirstOrderLogic

A library for parsing and reasoning over first-order logic: a parser, an AST,
equivalence transformations, normal forms (PNF/CNF/Skolem), a resolution theorem
prover with unification, forward/backward/abductive chaining, theory comparison,
model-theoretic semantics, kernel sets for explainability, and a WalkSAT solver for
the propositional fragment.

## Features

- **Parser** with readable ASCII syntax and operator aliases.
- **Transformations**: simplification, implication/biconditional elimination, De Morgan,
  quantifier pulling, distribution.
- **Normal forms**: Prenex (PNF), Conjunctive (CNF), Skolem.
- **Resolution** theorem prover with unification (optional clause subsumption).
- **Forward chaining**: `Saturate`/`Entails` fire rules to a fixpoint, with stratified
  negation-as-failure; unsafe rules and function symbols in rule heads are rejected up front.
- **Backward chaining**: goal-driven proof search with backtracking, NAF via failure
  sub-proofs, and a depth bound against cyclic rules.
- **Abductive chaining**: `Explain` yields the minimal sets of assumed ground literals
  (over declared abducible predicates) that would make an observation derivable.
- **Theory**: belief-base wrapper with `Entails`, consistency checks, and `Compare`
  against another theory — returns a `Stance` (agreements / disagreements / silences),
  in `Syntactic` or `Semantic` `ComparisonMode`.
- **Signature**: declare predicate/function/constant symbols with arities and validate
  sentences against the declaration.
- **Semantics / Interpretation**: model-theoretic evaluation over a domain of discourse,
  including quantifier evaluation by ranging over the domain's elements.
- **Kernel sets**: minimal entailing subsets of a belief base (Hansson 1994).
- **WalkSAT** for propositional clause sets.

## Syntax

- Conjunction — `AND`, `&&`
- Disjunction — `OR`, `||`
- Implication — `IMPLIES`, `=>`
- Biconditional — `IFF`, `<=>`
- Negation — `NOT`, `!`, `-`, `~`, `¬`
- Negation as failure — `NAF` (used by the chaining engines)
- Quantifiers — `FORALL x ...`, `EXISTS x ...`
- Constants — `TRUE`, `FALSE`

Exactly the identifiers `x`, `y`, `z`, `w` are free variables; every other identifier
(including e.g. `x1` or `yPos`) is a constant unless it is bound by an enclosing
quantifier, which makes it a variable within that scope.

Operator precedence, tightest first: `NOT` / `NAF` / quantifiers, then `AND`, then `OR`,
then `IMPLIES`, then `IFF`. `AND` and `OR` are left-associative; `IMPLIES` and `IFF` are
right-associative. Parentheses override as usual.

## Example

```csharp
using FirstOrderLogic;

var logic = new FirstOrderLogic.FirstOrderLogic();

var kb = (ISentence)logic.Parse("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))");
var skolem = kb.ToPrenexForm(out _).SkolemForm();
var goal = (ISentence)logic.Parse("Mortal(Sokrates)");

bool entailed = Resolution.Resolve(skolem, goal); // True
```

Free variables in the *goal* are query variables — `Resolve` asks whether some instance is
entailed (`Q(x)` asks "does Q hold for some x?"), so `Q(x)` and `FORALL x (Q(x))` are
different questions. In KB sentences free variables stay implicitly universal.

`Resolution.Resolve(kb, goal, useSubsumption: true)` enables clause subsumption (helps on
large, redundant problems; off by default). `Resolution.Resolve(kb, goal, maxRounds: n)` bounds the
saturation loop — FOL entailment is only semi-decidable, so a non-entailed KB with
function symbols can otherwise grow clauses forever; exceeding the budget throws
`InvalidOperationException`. A runnable version lives in
`examples/FirstOrderLogic.Example/Program.cs`.

## Kernel sets

A **kernel** is a minimal subset of a belief base `K` that entails `α`; multiple kernels
expose independent proof paths. Entailment delegates to `Resolution`.

```csharp
var ks = new KernelSets();
var K = new List<ISentence> {
    (ISentence)logic.Parse("P(a)"),
    (ISentence)logic.Parse("P(x) => Q(x)"),
    (ISentence)logic.Parse("R(b)"),   // irrelevant to Q(a)
};
var α = (ISentence)logic.Parse("Q(a)");

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

A runnable version lives in `examples/AIPlanning.Example/Program.cs`.

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
  `tests/AIPlanning.Tests/MultiAgentPlanningTests.cs`; works for small N,
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

The papers in `docs/papers/` are the working references:

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
