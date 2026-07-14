using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LRParser.Language;

namespace FirstOrderLogic {
    // A sentence mentions a symbol the interpretation does not cover — distinguishable from
    // genuine programming errors, so callers can skip such sentences without swallowing bugs.
    public class InterpretationException : Exception {
        public InterpretationException(string message) : base(message) { }
    }

    public class PossibleWorld : ILanguageObject{
        public readonly Dictionary<IProposition, bool> _propositionalAssignment;

        // Copies the assignment: builders reuse (and Clear()) their dictionaries between
        // builds, and a world's identity must not change under the caller's hands.
        public PossibleWorld(Dictionary<IProposition, bool> propositionalAssignment) {
            _propositionalAssignment = new Dictionary<IProposition, bool>(propositionalAssignment);
        }
    
        public void Switch(IProposition proposition) {
            _propositionalAssignment[proposition] = !_propositionalAssignment[proposition];
        }
    
        public virtual bool Evaluate(ISentence sentence) {
            return sentence switch {
                AtomicSentence atomicSentence => Evaluate(atomicSentence),
                ComplexSentence complexSentence => Evaluate(complexSentence),
                _ => throw new Exception($"Error: subtype of {this} not found.")
            };
        }
    
        protected virtual bool Evaluate(IComplexSentence complexSentence) {
            // No TRUE/FALSE connective cases: the parser emits ⊤/⊥ as Propositions
            // (handled via Tautology/Contradiction below), never as connectives.
            return complexSentence.Connective.Symbol switch {
                Connective.LogicSymbol.NEGATION => !Evaluate(complexSentence.Children[0]),
                Connective.LogicSymbol.CONJUNCTION => Evaluate(complexSentence.Children[0]) && Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.DISJUNCTION => Evaluate(complexSentence.Children[0]) || Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.IMPLICATION => !Evaluate(complexSentence.Children[0]) || Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.BICONDITIONAL => Evaluate(complexSentence.Children[0]) == Evaluate(complexSentence.Children[1]),
                // A connective without classical truth conditions (e.g. NAF) is a sentence
                // this interpretation does not cover — the skippable category, not a bug.
                _ => throw new InterpretationException($"Error: connective {complexSentence.Connective.Symbol} cannot be evaluated in a possible world.")
            };
        }
    
        protected virtual bool Evaluate(IAtomicSentence atomicSentence) {
            return atomicSentence switch {
                Proposition proposition => Evaluate(proposition),
                _ => throw new InterpretationException($"Error: {atomicSentence} not found in interpretation.")
            };
        }

        private bool Evaluate(IProposition proposition) {
            if (proposition.Tautology) return true;
            if (proposition.Contradiction) return false;

            if (_propositionalAssignment.TryGetValue(proposition, out var value)) {
                return value;
            }

            throw new InterpretationException($"Error: {proposition} not found in interpretation.");
        }
    
        public bool Evaluate(List<Clause> clauseSet) {
            return clauseSet.All(Evaluate);
        }
    
        public bool Evaluate(Clause clause) {
            return clause.Literals.Any(Evaluate);        
        }
    
        public override string ToString()
        {
            var output = new StringBuilder();
            foreach (var (key, value) in _propositionalAssignment) {
                output.Append($"{key}={value}, ");
            }

            return output.ToString();
        }

        public PossibleWorld Clone() {
            return new PossibleWorld(_propositionalAssignment);
        }
    }

    public class Interpretation : PossibleWorld {
        private IDomainOfDiscourse Domain { get; }
        private readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> _relations = new();
        private readonly Dictionary<string, Func<Term[], IElementOfDiscourse>> _functions = new();
        private readonly Dictionary<string, IElementOfDiscourse> _variableAssigment = new();
    
        // Tables are copied for the same reason as in the base ctor.
        public Interpretation(IDomainOfDiscourse domain,
            Dictionary<string, Func<IElementOfDiscourse[], bool>> relations,
            Dictionary<string, Func<Term[], IElementOfDiscourse>> functions,
            Dictionary<string, IElementOfDiscourse> variableAssigment,
            Dictionary<IProposition, bool> propositionalAssignment) : base(propositionalAssignment) {

            Domain = domain;
            _relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>>(relations);
            _functions = new Dictionary<string, Func<Term[], IElementOfDiscourse>>(functions);
            _variableAssigment = new Dictionary<string, IElementOfDiscourse>(variableAssigment);
        }
    
        public IElementOfDiscourse EvaluateTerm(Term term) => Evaluate(term);
        
        public override bool Evaluate(ISentence sentence) {
            if(sentence.HasScopeConflict()) {
                throw new Exception("Error: Sentence has scope conflict.");
            }
        
            return base.Evaluate(sentence);
        }
    
        protected override bool Evaluate(IComplexSentence complexSentence) {
            return complexSentence.Connective.Symbol switch {
                Connective.LogicSymbol.UNIVERSAL => Domain.Elements.All(element => EvaluateBoundTo(complexSentence, element)),
                Connective.LogicSymbol.EXISTENTIAL => Domain.Elements.Any(element => EvaluateBoundTo(complexSentence, element)),
                _ => base.Evaluate(complexSentence)
            };
        }

        // The finally is required: SemanticChaining.Detach catches evaluation exceptions and
        // keeps using the interpretation, so a binding must not survive a throwing body.
        private bool EvaluateBoundTo(IComplexSentence quantified, IElementOfDiscourse element) {
            var variable = ((Quantifier)quantified.Connective).Variable;
            var hadOuter = _variableAssigment.TryGetValue(variable.TermSymbol, out var outer);
            _variableAssigment[variable.TermSymbol] = element;
            try {
                return base.Evaluate(quantified.Children[0]);
            }
            finally {
                if (hadOuter) {
                    _variableAssigment[variable.TermSymbol] = outer!;
                }
                else {
                    _variableAssigment.Remove(variable.TermSymbol);
                }
            }
        }
    
        protected override bool Evaluate(IAtomicSentence atomicSentence) {
            return atomicSentence switch {
                Predicate predicate => Evaluate(predicate),
                _ => base.Evaluate(atomicSentence)
            };
        }
    
        private bool Evaluate(IPredicate predicate) {
            if(!_relations.TryGetValue(predicate.Symbol, out var relation)) {
                throw new InterpretationException($"Error: {predicate} not found in interpretation.");
            }

            return relation(Array.ConvertAll(predicate.Terms, Evaluate));
        }
    
        private IElementOfDiscourse Evaluate(Term term) {
            return term switch {
                // Constants are arity-0 functions: function.Terms is empty for them.
                Function function =>  _functions.TryGetValue(function.TermSymbol, out var func) ? func(function.Terms) : throw new InterpretationException("Error: function not found in interpretation."),
                Variable variable => _variableAssigment.TryGetValue(variable.TermSymbol, out var domain) ? domain : throw new InterpretationException("Error: variable not found in interpretation."),
                _ => throw new InterpretationException($"Error: {term} not found in interpretation.")
            };
        }

    }
}