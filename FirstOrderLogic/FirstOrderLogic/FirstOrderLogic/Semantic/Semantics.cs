using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Builds an Interpretation over a fixed Signature, enforcing that every declared symbol is
    // interpreted (predicate → relation, constant → function). Rule-derived predicates are exempt.
    // Concrete subclasses fill the tables in Define().
    public abstract class Semantics
    {
        protected readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> Relations = new();
        protected readonly Dictionary<string, Func<Term[], IElementOfDiscourse>> Functions = new();
        protected readonly Dictionary<string, IElementOfDiscourse> VariableAssignments = new();
        protected readonly Dictionary<IProposition, bool> PropositionalAssignments = new();

        protected abstract Signature Signature { get; }

        // Predicates produced by rules rather than read extensionally — no relation required.
        protected virtual IReadOnlyCollection<string> DerivedPredicates => Array.Empty<string>();

        // Populate Relations / Functions / assignments for the current context.
        protected abstract void Define();

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

        // Functions may exceed the signature (domains supply individuals dynamically), so only
        // coverage of declared constants is checked, not strays.
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
