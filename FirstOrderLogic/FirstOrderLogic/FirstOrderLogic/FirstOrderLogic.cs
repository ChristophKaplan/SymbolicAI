using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LRParser.CFG;
using LRParser.Language;
using LRParser.Lexer;

namespace FirstOrderLogic {
    public enum Terminal
    {
        Open,
        Comma,
        Close,
        Identifier,
        Conjunction,
        Disjunction,
        Implication,
        Negation,
        Boolean,
        Quantifier,
        Biconditional,
        TimeAttribute,
        Naf
    }

    public enum NonTerminal
    {
        LangObject,
        AtomicSentence,
        Term,
        TermList,
        Sentence,
        IffSentence,
        ImpliesSentence,
        OrSentence,
        AndSentence,
        UnarySentence,
        PrimarySentence
    }

    public class FirstOrderLogic : Language<Terminal, NonTerminal>
    {
        protected override TokenDefinition<Terminal>[] SetUpTokenDefinitions()
        {
            return new[]
            {
                new TokenDefinition<Terminal>(Terminal.Open, "\\("),
                new TokenDefinition<Terminal>(Terminal.Comma, ","),
                new TokenDefinition<Terminal>(Terminal.Close, "\\)"),
                new TokenDefinition<Terminal>(Terminal.Conjunction, "AND|&&|\u2227"),
                new TokenDefinition<Terminal>(Terminal.Disjunction, "OR|\\|\\||\u2228"),
                new TokenDefinition<Terminal>(Terminal.Implication, "IMPLIES|=>|\u21d2"),
                new TokenDefinition<Terminal>(Terminal.Biconditional, "IFF|<=>|\u21d4"),
                new TokenDefinition<Terminal>(Terminal.Negation, "NOT|!|-|~|\u00ac"),
                new TokenDefinition<Terminal>(Terminal.Boolean, "TRUE|FALSE|\u22a4|\u22a5"),
                new TokenDefinition<Terminal>(Terminal.Quantifier, "FORALL|EXISTS|\u2200|\u2203"),
                new TokenDefinition<Terminal>(Terminal.Naf, "NAF"),
                new TokenDefinition<Terminal>(Terminal.TimeAttribute, "\\^[0-9]+"),
                // Unicode letters/digits, not just ASCII, so non-English symbol names (Wählt, Citté,
                // 本) are valid identifiers. Keyword tokens above are matched first, and logical
                // operator glyphs (∧ ¬ ∀ …) are Symbol-category, so neither collides with this.
                new TokenDefinition<Terminal>(Terminal.Identifier, @"[\p{L}\p{N}]+"),
            };
        }

        // Precedence, tightest first: NOT/NAF/quantifiers, then AND, then OR, then
        // IMPLIES, then IFF. AND and OR are left-associative (left-recursive rules,
        // fine for an LALR parser); IMPLIES and IFF are right-associative.
        protected override void SetUpGrammar()
        {
            AddRule(rhs => rhs[0].Attribute, NonTerminal.LangObject, NonTerminal.Sentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.Sentence, NonTerminal.IffSentence);

            AddRule(Binary(Connective.LogicSymbol.BICONDITIONAL),
                NonTerminal.IffSentence, NonTerminal.ImpliesSentence, Terminal.Biconditional, NonTerminal.IffSentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.IffSentence, NonTerminal.ImpliesSentence);

            AddRule(Binary(Connective.LogicSymbol.IMPLICATION),
                NonTerminal.ImpliesSentence, NonTerminal.OrSentence, Terminal.Implication, NonTerminal.ImpliesSentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.ImpliesSentence, NonTerminal.OrSentence);

            AddRule(Binary(Connective.LogicSymbol.DISJUNCTION),
                NonTerminal.OrSentence, NonTerminal.OrSentence, Terminal.Disjunction, NonTerminal.AndSentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.OrSentence, NonTerminal.AndSentence);

            AddRule(Binary(Connective.LogicSymbol.CONJUNCTION),
                NonTerminal.AndSentence, NonTerminal.AndSentence, Terminal.Conjunction, NonTerminal.UnarySentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.AndSentence, NonTerminal.UnarySentence);

            AddRule(Unary(Connective.LogicSymbol.NEGATION),
                NonTerminal.UnarySentence, Terminal.Negation, NonTerminal.UnarySentence);
            AddRule(Unary(Connective.LogicSymbol.NAF),
                NonTerminal.UnarySentence, Terminal.Naf, NonTerminal.UnarySentence);

            AddRule(rhs =>
            {
                var quantifierSymbol = ((LexValue)rhs[0].Attribute).ToLogicalConstant();
                var variable = new Variable(((LexValue)rhs[1].Attribute).Value);
                var body = BindConstantsToVariable((ISentence)rhs[2].Attribute, variable);
                return new ComplexSentence(new Quantifier(quantifierSymbol, variable), body);
            }, NonTerminal.UnarySentence, Terminal.Quantifier, Terminal.Identifier, NonTerminal.UnarySentence);

            AddRule(rhs => rhs[0].Attribute, NonTerminal.UnarySentence, NonTerminal.PrimarySentence);

            AddRule(rhs => rhs[1].Attribute, NonTerminal.PrimarySentence, Terminal.Open, NonTerminal.Sentence, Terminal.Close);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.PrimarySentence, NonTerminal.AtomicSentence);
            AddRule(rhs =>
            {
                var boolean = ((LexValue)rhs[0].Attribute).ToLogicalConstant();
                return new Proposition(Connective.SymbolToString(boolean));
            }, NonTerminal.PrimarySentence, Terminal.Boolean);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                return new Proposition(symbol);
            }, NonTerminal.AtomicSentence, Terminal.Identifier);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                var timeValue = int.Parse(((LexValue)rhs[1].Attribute).Value[1..]);
                return new Proposition(symbol, timeValue);
            }, NonTerminal.AtomicSentence, Terminal.Identifier, Terminal.TimeAttribute);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                var terms = ((ArrayValue)rhs[2].Attribute).Value.Select(lo => (Term)lo).ToArray();
                return new Predicate(symbol, terms);
            }, NonTerminal.AtomicSentence, Terminal.Identifier, Terminal.Open, NonTerminal.TermList, Terminal.Close);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                var terms = ((ArrayValue)rhs[2].Attribute).Value.Select(lo => (Term)lo).ToArray();
                var timeValue = int.Parse(((LexValue)rhs[4].Attribute).Value[1..]);
                return new Predicate(symbol, terms, timeValue);
            }, NonTerminal.AtomicSentence, Terminal.Identifier, Terminal.Open, NonTerminal.TermList, Terminal.Close, Terminal.TimeAttribute);

            AddRule(rhs => new ArrayValue(rhs[0].Attribute), NonTerminal.TermList, NonTerminal.Term);
            AddRule(rhs =>
            {
                var list = (ArrayValue)rhs[0].Attribute;
                list.Add(rhs[2].Attribute);
                return list;
            }, NonTerminal.TermList, NonTerminal.TermList, Terminal.Comma, NonTerminal.Term);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                var variableList = new[] { "x", "y", "z", "w" };
                return variableList.Contains(symbol)
                    ? (ILanguageObject)new Variable(symbol)
                    : new Constant(symbol);
            }, NonTerminal.Term, Terminal.Identifier);

            AddRule(rhs =>
            {
                var symbol = ((LexValue)rhs[0].Attribute).Value;
                var terms = ((ArrayValue)rhs[2].Attribute).Value.Select(lo => (Term)lo).ToArray();
                return new Function(symbol, terms);
            }, NonTerminal.Term, Terminal.Identifier, Terminal.Open, NonTerminal.TermList, Terminal.Close);
        }

        private static Production.SemanticActionDelegate Binary(Connective.LogicSymbol symbol) =>
            rhs => new ComplexSentence((ISentence)rhs[0].Attribute, symbol, (ISentence)rhs[2].Attribute);

        private static Production.SemanticActionDelegate Unary(Connective.LogicSymbol symbol) =>
            rhs => new ComplexSentence(symbol, (ISentence)rhs[1].Attribute);

        // A quantifier may bind any identifier, but the term rule only whitelists
        // {x,y,z,w} as variables; every other identifier is parsed as a Constant.
        // On reducing a quantifier, rewrite the constants it binds into its variable.
        // Scoping falls out of bottom-up evaluation: an inner quantifier over the
        // same name has already converted its occurrences to Variables, which this
        // type-precise rewrite leaves untouched.
        private static ISentence BindConstantsToVariable(ISentence sentence, Variable variable)
        {
            switch (sentence)
            {
                case Predicate predicate:
                {
                    var terms = predicate.Terms.Select(term => BindTerm(term, variable)).ToArray();
                    return predicate.Time.HasValue
                        ? new Predicate(predicate.Symbol, terms, predicate.Time.Value)
                        : new Predicate(predicate.Symbol, terms);
                }
                case ComplexSentence complex:
                    return complex.WithChildren(complex.Children
                        .Select(child => BindConstantsToVariable(child, variable)).ToList());
                default:
                    return sentence;
            }
        }

        private static Term BindTerm(Term term, Variable variable)
        {
            return term switch
            {
                Constant constant => constant.TermSymbol == variable.TermSymbol ? variable : term,
                Function { Arity: > 0 } function =>
                    new Function(function.TermSymbol, function.Terms.Select(t => BindTerm(t, variable)).ToArray()),
                _ => term
            };
        }

        /// <summary>
        /// Misleadingly named: this THROWS on invalid input (it is a plain alias for the
        /// inherited <see cref="LRParser.Language.Language{T,N}.Parse(string)"/>, which is the
        /// properly-named entry point — prefer calling <c>Parse</c> directly). The inherited
        /// <c>TryParse(string, out ILanguageObject)</c> overload provides real try-semantics.
        /// Kept because the name is baked into many call sites across dependent projects
        /// (AIPlanning, examples, docs); renaming would churn them all for no behavior change.
        /// </summary>
        public ILanguageObject TryParse(string input) => base.Parse(input);

        public List<ILanguageObject> TryParse(List<string> inputList)
        {
            var langObjList = new List<ILanguageObject>();
            foreach (var input in inputList) {
                langObjList.Add(TryParse(input));
            }
            return langObjList;
        }
    }
}