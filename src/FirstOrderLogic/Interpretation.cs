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
        private readonly Dictionary<IProposition, bool> _propositionalAssignment;

        // Copies the assignment: builders reuse (and Clear()) their dictionaries between
        // builds, and a world's identity must not change under the caller's hands.
        public PossibleWorld(Dictionary<IProposition, bool> propositionalAssignment) {
            _propositionalAssignment = new Dictionary<IProposition, bool>(propositionalAssignment);
        }
    
        public void Toggle(IProposition proposition) {
            _propositionalAssignment[proposition] = !_propositionalAssignment[proposition];
        }
    
        public virtual bool Evaluate(ISentence sentence) {
            return sentence switch {
                AtomicSentence atomicSentence => Evaluate(atomicSentence),
                ComplexSentence complexSentence => Evaluate(complexSentence),
                _ => throw new NotSupportedException($"Error: subtype of {sentence} not found.")
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
            if (proposition.Tautology)
            {
                return true;
            }

            if (proposition.Contradiction)
            {
                return false;
            }

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
        private readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> _relations;
        private readonly Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>> _functions;
        private readonly Dictionary<string, IElementOfDiscourse> _variableAssignment;

        // Tables are copied for the same reason as in the base ctor.
        public Interpretation(IDomainOfDiscourse domain,
            Dictionary<string, Func<IElementOfDiscourse[], bool>> relations,
            Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>> functions,
            Dictionary<string, IElementOfDiscourse> variableAssignment,
            Dictionary<IProposition, bool> propositionalAssignment) : base(propositionalAssignment) {

            Domain = domain;
            _relations = new Dictionary<string, Func<IElementOfDiscourse[], bool>>(relations);
            _functions = new Dictionary<string, Func<IElementOfDiscourse[], IElementOfDiscourse>>(functions);
            _variableAssignment = new Dictionary<string, IElementOfDiscourse>(variableAssignment);
        }
    
        public IElementOfDiscourse EvaluateTerm(Term term) => Evaluate(term);
        
        public override bool Evaluate(ISentence sentence) {
            if(sentence.HasScopeConflict()) {
                throw new ArgumentException("Error: Sentence has scope conflict.", nameof(sentence));
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
            var hadOuter = _variableAssignment.TryGetValue(variable.TermSymbol, out var outer);
            _variableAssignment[variable.TermSymbol] = element;
            try {
                return base.Evaluate(quantified.Children[0]);
            }
            finally {
                if (hadOuter) {
                    _variableAssignment[variable.TermSymbol] = outer!;
                }
                else {
                    _variableAssignment.Remove(variable.TermSymbol);
                }
            }
        }
    
        protected override bool Evaluate(IAtomicSentence atomicSentence) {
            if (atomicSentence is Predicate predicate) {
                return Evaluate(predicate);
            }

            // A 0-ary predicate parses as a Proposition, so the only way its declared relation is
            // ever reachable is from the propositional path — applied to no arguments.
            if (atomicSentence is IProposition { IsNullaryConstant: false } proposition &&
                _relations.TryGetValue(proposition.Symbol, out var relation)) {
                return relation(Array.Empty<IElementOfDiscourse>());
            }

            return base.Evaluate(atomicSentence);
        }
    
        private bool Evaluate(IPredicate predicate) {
            if(!_relations.TryGetValue(predicate.Symbol, out var relation)) {
                throw new InterpretationException($"Error: {predicate} not found in interpretation.");
            }

            return relation(predicate.Terms.Select(Evaluate).ToArray());
        }
    
        private IElementOfDiscourse Evaluate(Term term) {
            return term switch {
                // Compositional, like relations: arguments are evaluated to domain elements first,
                // so an interpreted function never has to resolve a variable binding itself.
                // Constants are arity-0 functions: function.Terms is empty for them.
                Function function =>  _functions.TryGetValue(function.TermSymbol, out var func) ? func(function.Terms.Select(Evaluate).ToArray()) : throw new InterpretationException("Error: function not found in interpretation."),
                Variable variable => _variableAssignment.TryGetValue(variable.TermSymbol, out var element) ? element : throw new InterpretationException("Error: variable not found in interpretation."),
                _ => throw new InterpretationException($"Error: {term} not found in interpretation.")
            };
        }

    }
}