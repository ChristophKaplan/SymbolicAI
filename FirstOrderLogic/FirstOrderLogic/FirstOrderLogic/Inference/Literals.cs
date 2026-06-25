using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // Helpers over literals (an atom or a negated atom) shared by the inference procedures.
    internal static class Literals
    {
        public static IEnumerable<Variable> VariablesOf(ISentence literal)
        {
            if (AtomOf(literal) is not IPredicate predicate) return Enumerable.Empty<Variable>();
            return predicate.GetVariables().Distinct();
        }

        // Polarity is part of the tag, so a positive literal never matches a negated one.
        public static string Signature(ISentence literal)
        {
            var atom = AtomOf(literal);
            var tag = atom is IPredicate predicate
                ? predicate.Symbol + "/" + predicate.Arity
                : ((IAtomicSentence)atom).Symbol + "/0";
            return literal.IsNegation ? "¬" + tag : tag;
        }

        public static string SymbolOf(ISentence literal)
        {
            var atom = AtomOf(literal);
            return atom is IPredicate predicate ? predicate.Symbol : ((IAtomicSentence)atom).Symbol;
        }

        private static ISentence AtomOf(ISentence literal) =>
            literal.IsNegation ? literal.Children[0] : literal;
    }
}
