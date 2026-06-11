using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Substitution plumbing shared by the chaining procedures: dereferencing terms through a binding
    // map, applying that map to a literal, unioning two maps, and unifying two literals into a map.
    // Kept internal — it is implementation detail of ForwardChaining / BackwardChaining, not part of
    // the public FOL vocabulary (which already exposes Unificator for one-shot unification).
    internal static class Bindings
    {
        // Dereference a term through the binding chain: follow variable→term links to the end, then
        // rebuild any function over its dereferenced arguments. The unifier's occurs-check rules out
        // cyclic bindings, so the recursion terminates.
        public static Term Walk(Term term, IReadOnlyDictionary<Variable, Term> theta)
        {
            switch (term)
            {
                case Variable v:
                    return theta.TryGetValue(v, out var bound) ? Walk(bound, theta) : v;
                case Function f when f.Arity > 0:
                    var args = new Term[f.Terms.Length];
                    for (var i = 0; i < args.Length; i++) args[i] = Walk(f.Terms[i], theta);
                    return new Function(f.TermSymbol, args);
                default:
                    return term; // constant (arity-0 function) or unbound variable handled above
            }
        }

        // A fresh clone of `literal` with every variable replaced by its fully-dereferenced binding.
        // Never mutates the input.
        public static ISentence Apply(ISentence literal, IReadOnlyDictionary<Variable, Term> theta)
        {
            var clone = literal.Clone();
            if (theta.Count == 0) return clone;
            foreach (var v in VariablesOf(clone).ToList())
            {
                var resolved = Walk(v, theta);
                if (!resolved.Equals(v)) clone.SubstituteTerm(v, resolved);
            }
            return clone;
        }

        // theta ∪ more, as a new map. Callers only extend with bindings for variables absent from theta
        // (the goal literal is pre-applied; fresh-clause variables are renamed unique), so the union is
        // conflict-free by construction.
        public static Dictionary<Variable, Term> Extend(
            IReadOnlyDictionary<Variable, Term> theta, IReadOnlyDictionary<Variable, Term> more)
        {
            var result = new Dictionary<Variable, Term>(theta.Count + more.Count);
            foreach (var pair in theta) result[pair.Key] = pair.Value;
            foreach (var pair in more) result[pair.Key] = pair.Value;
            return result;
        }

        // The distinct variables occurring in a literal (positive or negated atom). Propositions and
        // ground atoms yield none.
        public static IEnumerable<Variable> VariablesOf(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            if (atom is not IPredicate predicate) return Enumerable.Empty<Variable>();
            return predicate.GetVariables().Distinct();
        }

        // Most-general unifier of two literals as a binding map, or false if they do not unify.
        public static bool TryUnify(ISentence a, ISentence b, out Dictionary<Variable, Term> mgu)
        {
            var unificator = new Unificator(a, b);
            if (!unificator.IsUnifiable)
            {
                mgu = new Dictionary<Variable, Term>();
                return false;
            }
            mgu = new Dictionary<Variable, Term>(unificator.Substitutions);
            return true;
        }

        // "Symbol/arity" tag for cheap pre-filtering before attempting a unify.
        public static string Signature(ISentence literal)
        {
            var atom = literal.IsNegation ? literal.Children[0] : literal;
            return atom is IPredicate predicate
                ? predicate.Symbol + "/" + predicate.Arity
                : ((IAtomicSentence)atom).Symbol + "/0";
        }
    }
}
