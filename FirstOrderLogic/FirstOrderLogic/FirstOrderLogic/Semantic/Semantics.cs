using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Builds an Interpretation over a fixed Signature and enforces that every declared symbol is
    // actually interpreted: a predicate by a relation (its extension), a constant by a function.
    // Predicates the surrounding theory *derives* by rules (rather than reads from a world) are
    // declared exempt — no extensional relation is expected for them. Concrete subclasses fill the
    // tables in Define() for a given context (e.g. one specific agent).
    //
    // This is the model-theoretic counterpart of Signature: the Signature says what can be said,
    // a Semantics says what those symbols mean, and the resulting Interpretation says what is true.
    public abstract class Semantics
    {
        protected readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> Relations = new();
        protected readonly Dictionary<string, Func<Term[], IElementOfDiscourse>> Functions = new();
        protected readonly Dictionary<string, IElementOfDiscourse> VariableAssignments = new();
        protected readonly Dictionary<IProposition, bool> PropositionalAssignments = new();

        // The vocabulary this semantics must interpret.
        protected abstract Signature Signature { get; }

        // Declared predicate symbols that are produced by rules rather than read extensionally, so no
        // relation is required (or expected) for them. Default: none.
        protected virtual IReadOnlyCollection<string> DerivedPredicates => Array.Empty<string>();

        // Populate Relations / Functions / assignments for the current context.
        protected abstract void Define();

        // Define, enforce the signature contract, then construct the Interpretation over `domain`.
        public Interpretation BuildInterpretation(IDomainOfDiscourse domain)
        {
            Relations.Clear();
            Functions.Clear();
            VariableAssignments.Clear();
            PropositionalAssignments.Clear();

            Define();
            Validate();

            return new Interpretation(
                domain, Relations, Functions, VariableAssignments, PropositionalAssignments);
        }

        // Signature ↔ tables contract:
        //   • every declared, non-derived predicate has a relation,
        //   • no relation is defined for an undeclared predicate (typo guard),
        //   • every declared constant has a function.
        // Functions may exceed the signature: a domain supplies its individuals (world entities)
        // dynamically, so we check function *coverage* of declared constants, not strays.
        private void Validate()
        {
            var problems = new List<string>();

            foreach (var predicate in Signature.Predicates.Keys)
                if (!DerivedPredicates.Contains(predicate) && !Relations.ContainsKey(predicate))
                    problems.Add($"no relation for declared predicate '{predicate}'");

            foreach (var relation in Relations.Keys)
                if (!Signature.HasPredicate(relation))
                    problems.Add($"relation '{relation}' is not declared in the signature");

            foreach (var constant in Signature.Constants)
                if (!Functions.ContainsKey(constant))
                    problems.Add($"no function for declared constant '{constant}'");

            if (problems.Count > 0)
                throw new Exception(
                    $"{GetType().Name} does not satisfy its signature:\n  - " +
                    string.Join("\n  - ", problems));
        }
    }
}
