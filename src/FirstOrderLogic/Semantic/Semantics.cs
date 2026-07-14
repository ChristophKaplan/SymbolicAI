using System;
using System.Collections.Generic;

namespace FirstOrderLogic
{
    // A first-order structure is total over its signature: every declared symbol must be
    // interpreted (predicate → relation, constant → function), enforced by Validate.
    public abstract class Semantics
    {
        protected readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> Relations = new();
        protected readonly Dictionary<string, Func<Term[], IElementOfDiscourse>> Functions = new();
        protected readonly Dictionary<string, IElementOfDiscourse> VariableAssignments = new();
        protected readonly Dictionary<IProposition, bool> PropositionalAssignments = new();

        protected abstract Signature Signature { get; }

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
                if (!Relations.ContainsKey(predicate))
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
