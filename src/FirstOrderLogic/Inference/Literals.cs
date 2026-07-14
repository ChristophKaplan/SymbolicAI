using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    internal static class Literals
    {
        public static IEnumerable<Variable> VariablesOf(this ISentence literal)
        {
            if (literal.AtomOf() is not IPredicate predicate)
            {
                return Enumerable.Empty<Variable>();
            }

            return predicate.GetVariables().Distinct();
        }

        // Polarity is part of the tag, so a positive literal never matches a negated one.
        public static string Signature(this ISentence literal)
        {
            var atom = literal.AtomOf();
            var tag = atom is IPredicate predicate
                ? predicate.Symbol + "/" + predicate.Arity
                : ((IAtomicSentence)atom).Symbol + "/0";
            return literal.IsNegation ? "¬" + tag : tag;
        }

        public static string SymbolOf(this ISentence literal)
        {
            var atom = literal.AtomOf();
            return atom is IPredicate predicate ? predicate.Symbol : ((IAtomicSentence)atom).Symbol;
        }

        public static ISentence AtomOf(this ISentence literal) =>
            literal.IsNegation || literal.IsNaf ? literal.Children[0].AtomOf() : literal;

        // Applies `fresh` to each distinct variable in first-occurrence order; a null result
        // leaves that variable untouched.
        public static ISentence Renamed(this ISentence literal, System.Func<Variable, Variable?> fresh)
        {
            foreach (var variable in literal.VariablesOf().ToList())
            {
                var replacement = fresh(variable);
                if (replacement != null)
                {
                    literal = literal.Substitute(variable, replacement);
                }
            }

            return literal;
        }
    }
}
