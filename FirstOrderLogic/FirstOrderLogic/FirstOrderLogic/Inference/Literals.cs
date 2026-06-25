using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Helpers over literals (an atom or a negated atom) shared by the inference procedures.
    internal static class Literals
    {
        public static IEnumerable<Variable> VariablesOf(this ISentence literal)
        {
            if (literal.AtomOf() is not IPredicate predicate) return Enumerable.Empty<Variable>();
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

        private static ISentence AtomOf(this ISentence literal) =>
            literal.IsNegation ? literal.Children[0] : literal;
    }
}
