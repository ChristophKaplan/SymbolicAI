using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LRParser.Language;

namespace FirstOrderLogic {
    public class PossibleWorld : ILanguageObject{
        public readonly Dictionary<IProposition, bool> _propositionalAssignment = new();
        public PossibleWorld(Dictionary<IProposition, bool> propositionalAssignment) {
            _propositionalAssignment = propositionalAssignment;
        }
    
        public void Assign(IProposition proposition, bool value) {
            _propositionalAssignment[proposition] = value;
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
            return complexSentence.Connective.Symbol switch {
                Connective.LogicSymbol.TRUE => true,
                Connective.LogicSymbol.FALSE => false,
                Connective.LogicSymbol.NEGATION => !Evaluate(complexSentence.Children[0]),
                Connective.LogicSymbol.CONJUNCTION => Evaluate(complexSentence.Children[0]) && Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.DISJUNCTION => Evaluate(complexSentence.Children[0]) || Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.IMPLICATION => !Evaluate(complexSentence.Children[0]) || Evaluate(complexSentence.Children[1]),
                Connective.LogicSymbol.BICONDITIONAL => Evaluate(complexSentence.Children[0]) == Evaluate(complexSentence.Children[1]),
                _ => throw new Exception($"Error: subtype of {complexSentence.Connective.Symbol} not found.")
            };
        }
    
        protected virtual bool Evaluate(IAtomicSentence atomicSentence) {
            return atomicSentence switch {
                Proposition proposition => Evaluate(proposition),
                _ => throw new Exception($"Error: {atomicSentence} not found in interpretation.")
            };
        }
    
        private bool Evaluate(IProposition proposition) {
            if (_propositionalAssignment.TryGetValue(proposition, out var value)) {
                return value;
            }
        
            throw new Exception($"Error: {proposition} not found in interpretation.");
        }
    
        public bool Evaluate(List<Clause> clauseSet) {
            return clauseSet.All(Evaluate);
        }
    
        public bool Evaluate(Clause clause) {
            return clause.Literals.Any(Evaluate);        
        }
    
        public override int GetHashCode() {
            var hash = 17;
            foreach (var kv in _propositionalAssignment) {
                var (key, value) = kv;
                hash = HashCode.Combine(hash ,key, value);
            }

            return hash;
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
            return new PossibleWorld(new Dictionary<IProposition, bool>(_propositionalAssignment));
        }
    }

    public class Interpretation : PossibleWorld {
        private IDomainOfDiscourse Domain { get; }
        private readonly Dictionary<string, Func<IElementOfDiscourse[], bool>> _relations = new();
        private readonly Dictionary<string, Func<Term[], IElementOfDiscourse>> _functions = new();
        private readonly Dictionary<string, IElementOfDiscourse> _variableAssigment = new();
    
        // The tables are copied: builders like Semantics reuse and Clear() their dictionaries
        // between builds, and quantifier evaluation adds synthetic constants — neither may leak
        // into (or out of) a previously built interpretation.
        public Interpretation(IDomainOfDiscourse domain,
            Dictionary<string, Func<IElementOfDiscourse[], bool>> relations,
            Dictionary<string, Func<Term[], IElementOfDiscourse>> functions,
            Dictionary<string, IElementOfDiscourse> variableAssigment,
            Dictionary<IProposition, bool> propositionalAssignment) : base(new Dictionary<IProposition, bool>(propositionalAssignment)) {

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
                Connective.LogicSymbol.UNIVERSAL => Domain.Elements.All(element => Evaluate(InstantiateVariable(((Quantifier)complexSentence.Connective).Variable, complexSentence.Children[0], element))),
                Connective.LogicSymbol.EXISTENTIAL => Domain.Elements.Any(element => Evaluate(InstantiateVariable(((Quantifier)complexSentence.Connective).Variable, complexSentence.Children[0], element))),
                _ => base.Evaluate(complexSentence)
            };
        }
    
        protected override bool Evaluate(IAtomicSentence atomicSentence) {
            return atomicSentence switch {
                Predicate predicate => Evaluate(predicate),
                _ => base.Evaluate(atomicSentence)
            };
        }
    
        private bool Evaluate(IPredicate predicate) {
            if(!_relations.TryGetValue(predicate.Symbol, out var relation)) {
                throw new Exception($"Error: {predicate} not found in interpretation.");
            }

            return relation(Array.ConvertAll(predicate.Terms, Evaluate));
        }
    
        private IElementOfDiscourse Evaluate(Term term) {
            return term switch {
                // Constants are arity-0 functions: function.Terms is empty for them.
                Function function =>  _functions.TryGetValue(function.TermSymbol, out var func) ? func(function.Terms) : throw new Exception("Error: function not found in interpretation."),
                Variable variable => _variableAssigment.TryGetValue(variable.TermSymbol, out var domain) ? domain : throw new Exception("Error: variable not found in interpretation."),
                _ => throw new Exception($"Error: {term} not found in interpretation.")
            };
        }

        // Substitution semantics: replace the bound variable with a fresh constant mapped to
        // `element`. The substituted body is the quantifier-stripped sentence to evaluate.
        private ISentence InstantiateVariable(Variable variable, ISentence body, IElementOfDiscourse element) {
            var constantToElement = new Constant($"{variable}_element_{element.Id}");
            _functions[constantToElement.TermSymbol] = _ => element;
            return body.Substitute(variable, constantToElement);
        }
    }
}