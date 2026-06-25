# Plan: Persistent (immutable) AST for FirstOrderLogic

Refactor the FOL `ISentence`/`Term` model from a mutable, parent-linked tree into an
immutable, top-down tree. Goal: remove the "mutation splices into the parent tree" hazard,
delete the pervasive `Clone()`, and make substitution a value-returning operation.

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

**Phase 0 — Safety net.** Add characterization tests for deep-nested transforms (PNF/CNF on
multi-level formulas) before trusting the suite.

**Phase 1 — Immutable Term layer.** `Function.Terms` truly readonly; add pure
`Term Substitute(Variable, Term)`; `Term.SubstituteAll` becomes a pure rebuild. Self-contained;
valuable even on its own.

**Phase 2 — Pure node ops, migrate callers.** Add `Substitute` / `Negated` / `WithChildren`
returning new nodes *alongside* the existing mutators. Point `Bindings`, `Resolution`, `Rule`
at the pure ops. No deletions yet — both APIs coexist so the build stays green.

**Phase 3 — Functional `TransformationFOL`.** Driver becomes
`ISentence Rewrite(ISentence, Func<ISentence,ISentence>)` (rebuild parent from transformed
children). Each rule: `void(ref ISentence)` -> `ISentence(ISentence)`. Delete the
`SetParentToParentOf` calls and in-place `Negate()`/`FlipOperator()` on children.

**Phase 4 — `Interpretation` environment-passing.** Carry the bound-variable set down the
evaluation recursion. `HasBoundVariables` disappears; the quantifier-strip becomes a
structural rebuild.

**Phase 5 — Remove the mutable surface.** With no readers left: delete `Parent`, `AddChild`,
`InsertChild`, `SetParentToParentOf`, in-place `SubstituteTerm`/`Negate`/`FlipOperator`/`AddTime`,
the `Symbol`/`Time` setters; make `Children` readonly; delete `Clone()`; make `Connective`
immutable. Large but mechanical (compiler-driven). This is the point of no return.

**Phase 6 — AIPlanning + optional perf.** Update `GpAction`/`OperatorGraph` (2-3 sites) to the
immutable API. Optional: hash-consing/interning pass; defer unless speed is wanted.

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
