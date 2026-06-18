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
        TimeAttribute
    }

    public enum NonTerminal
    {
        LangObject,
        AtomicSentence,
        Term,
        TermList,
        Sentence,
        ComplexSentence,
        LogicalOperator,
        ComplexSentenceUnary
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
                new TokenDefinition<Terminal>(Terminal.TimeAttribute, "\\^[0-9]"),
                // Unicode letters/digits, not just ASCII, so non-English symbol names (Wählt, Citté,
                // 本) are valid identifiers. Keyword tokens above are matched first, and logical
                // operator glyphs (∧ ¬ ∀ …) are Symbol-category, so neither collides with this.
                new TokenDefinition<Terminal>(Terminal.Identifier, @"[\p{L}\p{N}]+"),
            };
        }

        protected override void SetUpGrammar()
        {
            AddRule(rhs => rhs[0].Attribute, NonTerminal.LangObject, NonTerminal.Sentence);

            AddRule(rhs => rhs[1].Attribute, NonTerminal.Sentence, Terminal.Open, NonTerminal.Sentence, Terminal.Close);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.Sentence, NonTerminal.ComplexSentence);
            AddRule(rhs => rhs[0].Attribute, NonTerminal.Sentence, NonTerminal.AtomicSentence);
            AddRule(rhs =>
            {
                var boolean = ((LexValue)rhs[0].Attribute).ToLogicalConstant();
                return new Proposition(Connective.SymbolToString(boolean));
            }, NonTerminal.Sentence, Terminal.Boolean);

            AddRule(rhs =>
            {
                var quantifierSymbol = ((LexValue)rhs[0].Attribute).ToLogicalConstant();
                var variableString = ((LexValue)rhs[1].Attribute).Value;
                var sentence = (Sentence)rhs[2].Attribute;
                return new ComplexSentence(new Quantifier(quantifierSymbol, new Variable(variableString)), sentence);
            }, NonTerminal.ComplexSentence, Terminal.Quantifier, Terminal.Identifier, NonTerminal.Sentence);

            AddRule(rhs =>
            {
                var atomic = (Sentence)rhs[0].Attribute;
                var extArray = (ArrayValue)rhs[1].Attribute;
                var connective = (Connective)extArray.Value[0];
                var sentence = (Sentence)extArray.Value[1];
                return new ComplexSentence(atomic, connective, sentence);
            }, NonTerminal.ComplexSentence, NonTerminal.AtomicSentence, NonTerminal.ComplexSentenceUnary);

            AddRule(rhs =>
            {
                var atomic = (Sentence)rhs[1].Attribute;
                var extArray = (ArrayValue)rhs[3].Attribute;
                var connective = (Connective)extArray.Value[0];
                var sentence = (Sentence)extArray.Value[1];
                return new ComplexSentence(atomic, connective, sentence);
            }, NonTerminal.ComplexSentence, Terminal.Open, NonTerminal.Sentence, Terminal.Close, NonTerminal.ComplexSentenceUnary);

            AddRule(rhs =>
            {
                var extArray = (ArrayValue)rhs[0].Attribute;
                var negation = (Connective)extArray.Value[0];
                var sentence = (Sentence)extArray.Value[1];
                return new ComplexSentence(negation, sentence);
            }, NonTerminal.ComplexSentence, NonTerminal.ComplexSentenceUnary);

            AddRule(rhs =>
            {
                var connective = (Connective)rhs[0].Attribute;
                var sentences = (Sentence)rhs[1].Attribute;
                return new ArrayValue(connective, sentences);
            }, NonTerminal.ComplexSentenceUnary, NonTerminal.LogicalOperator, NonTerminal.Sentence);

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

            AddRule(GetConnective, NonTerminal.LogicalOperator, Terminal.Conjunction);
            AddRule(GetConnective, NonTerminal.LogicalOperator, Terminal.Disjunction);
            AddRule(GetConnective, NonTerminal.LogicalOperator, Terminal.Implication);
            AddRule(GetConnective, NonTerminal.LogicalOperator, Terminal.Biconditional);
            AddRule(GetConnective, NonTerminal.LogicalOperator, Terminal.Negation);
        }

        ILanguageObject GetConnective(Symbol[] rhs) => new Connective(((LexValue)rhs[0].Attribute).ToLogicalConstant());

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