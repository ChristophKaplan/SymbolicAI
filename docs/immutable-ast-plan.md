# Plan: Persistent (immutable) AST for FirstOrderLogic

Refactor the FOL `ISentence`/`Term` model from a mutable, parent-linked tree into an
immutable, top-down tree. Goal: remove the "mutation splices into the parent tree" hazard,
delete the pervasive `Clone()`, and make substitution a value-returning operation.

**Status: complete.** All phases landed on branch `immutable-ast`; FOL + AIPlanning suites
green throughout. The AST is now immutable (no parent pointers, no mutators, no setters,
structural sharing). Commits: d96c0b3, 2815b47, d6826b3, 0af9487, c2c5c03, ffe30cf, ab104e9.

## Scope

- **In:** `FirstOrderLogic` core (`FormulaClasses`, `TransformationFOL`, `Resolution`,
  `Interpretation`, `Bindings`, `Unificator`), plus the few `AIPlanning` call sites.
- **Out:** the LRParser sibling repo. It is generic over the empty `ILanguageObject`
  marker and builds bottom-up via value-returning semantic actions; the FOL grammar already
  constructs nodes via constructors. No parser change is required.

## Target design

| Concern | Now | Target |
|---|---|---|
| Tree links | `Parent { get; set; }` bidirectional | no parent pointers; top-down only |
| Children | mutable `List<ISentence>` | `IReadOnlyList<ISentence>`, set in ctor |
| Substitution | `void SubstituteTerm(...)` mutates | `ISentence Substitute(...)` returns new |
| Negation | `Negate()` splices into parent | `Negated()` returns new node |
| `FlipOperator` / `AddTime` / `Symbol` / `Time` | in-place setters | construct-new / get-only |
| `Function.Terms` | elements overwritten in place | readonly; substitution rebuilds |
| `Clone()` | everywhere (forced by parent pointers) | deleted; nodes are freely shared |
| `Connective` | mutable `Symbol` | immutable |

Equality/hashcode stay structural (already are), so the existing 223-test suite remains the
safety net.

## Key facts that make this tractable

- The only genuine upward navigation via `Parent` is two spots, both in `Interpretation.cs`:
  `Predicate.HasBoundVariables()` (used at ~line 139) and the quantifier-strip (~line 164).
  Everything else touching `Parent` / `SetParentToParentOf` is in-place rewrite plumbing that
  a functional traversal replaces.
- `TransformationFOL` already rewrites by building new nodes; it only leans on
  `SetParentToParentOf` to reassemble. Its rules port almost verbatim.
- Note: `BottomUpTransformation`'s `ref` threading through the child loop is inert (it
  reassigns a local copy); reassembly today relies entirely on `SetParentToParentOf`. Treat
  nested-transform coverage as suspect until Phase 0.

## Phases (each independently shippable, ordered by dependency)

**Phase 0 — Safety net. [done, d96c0b3]** Characterization tests for deep-nested transforms
(PNF/CNF on multi-level formulas). They passed against the old code, confirming
`SetParentToParentOf` reassembly worked and pinning that behavior as the contract.

**Phase 1 — Immutable Term layer. [done, d96c0b3]** Pure `Term.Substitute` returning a new term
(sharing unchanged subterms); `Predicate.SubstituteTerm` rebuilt on top of it; the in-place
`Term.SubstituteAll` / `Function.SubstituteTerm` removed.

**Phase 2 — Pure node ops, migrate callers. [done, 2815b47]** Pure `ISentence.Substitute`
alongside the mutator; `Bindings.Apply`, `Resolution`, `Rule` routed through it.

**Phase 3 — Functional `TransformationFOL`. [done, d6826b3]** Driver rebuilds parents from
transformed children via `WithChildren`; each rule returns a new sentence; all
`SetParentToParentOf` and in-place `Negate()`/`FlipOperator()` gone. Added `Negated()`.

**Phase 4 — `Interpretation` environment-passing. [done, 0af9487]** `InstantiateVariable` uses
pure `Substitute`; the vacuous `HasBoundVariables` Parent-walk removed. Last upward Parent
navigation gone.

**Phase 5a — Migrate consumers off mutators. [done, c2c5c03]** Pure `WithTimeShift`;
`GetInstancesOverTime`/`Resolution`/tests moved off `Negate()`/`AddTime()`. No external callers
of the splice/in-place mutators remained.

**Phase 5b — Remove parent pointers + mutators. [done, 56bfef2]** Deleted `Parent`,
`AddChild`/`InsertChild`/`SetParentToParentOf`, `Negate()`, `AddTime`, `FlipOperator`;
`Children` is now `IReadOnlyList` set at construction. Plus `Symbol`/`Time` setters made
get-only (`Negated` builds the flipped constant fresh). The AST became structurally immutable.

**Phase 5c-1 — Remove the `SubstituteTerm` mutator. [done, ffe30cf]** `Unificator` gained a pure
`Apply`; `GpAction`/`Resolution`/`SkolemForm` rebuild via pure `Substitute`. The AST now has no
mutators of any kind.

**Phase 5c-2 — Retire `Clone()`. [done, ab104e9]** Replaced every `ISentence`/`Term` `.Clone()`
with structural sharing and deleted the `Clone()` methods + copy constructors.

## Phase 6 — AIPlanning + perf

- **AIPlanning migration: done** (folded into 5a/5c). `GpAction.SpecifyAction` rebuilds via the
  pure `Unificator.Apply`; the copy ctor copies a list of shared immutable sentences. No
  mutable-sentence API remains in the planner.
- **Hash-consing / perf pass: evaluated and declined.** Implemented the two immutability-enabled
  optimizations — a `ReferenceEquals` fast-path in `Equals` and memoized `GetHashCode` — and
  measured them on `PerfBench`. The deltas were within run-to-run noise: this workload is bound
  by pair-iteration, normal-form conversion, and the `StructuralKey` `ToString` allocations in
  canonicalization, not by hashing/equality. Reverted to avoid unjustified complexity (mutable
  cache fields on otherwise-pure types). If a real bottleneck appears on large FOL problems, the
  profitable targets are the canonicalization keys and skipping canonicalization for variable-free
  clauses — not node interning.

## Not done (deliberately left)

- **`Connective` immutability** — `Connective.Symbol` is still a public field. Nothing mutates
  it (FlipOperator is gone), so it's effectively immutable; making it readonly is cosmetic.

## Effort & risk

- ~3-5 focused days. Phases 1-4 are additive and individually mergeable; Phase 5 is the
  breaking step.
- Biggest risk: the suspected nested-transform coverage gap (Phase 0 mitigates).
- Secondary: `Connective` mutability and the `AtomicSentence.Symbol` TRUE/FALSE flip in
  `Negate` — both handled by construct-new.

## Implementation note

Keep new code sparsely commented. The current codebase is comment-heavy; the immutable
rewrite should rely on clear names and small methods, reserving comments for genuinely
non-obvious invariants or hazards.
