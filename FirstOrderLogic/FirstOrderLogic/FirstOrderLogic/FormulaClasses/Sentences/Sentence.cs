using System;
using System.Collections.Generic;
using System.Linq;
using LogHelper;
using LRParser.Language;

namespace FirstOrderLogic {
    public interface ISentence : ILanguageObject {
        ISentence? Parent { get; set; }
        List<ISentence> Children { get; }
        bool IsBinary { get; }
        bool IsUnary { get; }
        bool IsNullary { get; }
        bool IsLiteral { get; }
        bool IsNegation { get; }
        bool IsImplication { get; }
        bool IsNegationOf(ISentence other, bool onlyPredSignature = false);
        void AddChild(ISentence sentence);
        void InsertChild(int index, ISentence sentence);
        void SetParentToParentOf(ISentence? parentOfThis);
        void SubstituteTerm(Term term, Term replacement);
        ISentence Clone();
        ISentence Negate();
        bool HasScopeConflict(List<Variable>? boundVariables = null);
        bool HasQuantifier();
        bool IsCNF();
        bool IsPropositional();
        bool IsGround();
        bool IsDisjunctionOfLiterals();
        List<ISentence> GetLiterals();
        IPredicate GetPredicate();
        IProposition GetProposition();
        void AddTime(int i);
        bool IsImplicationAndEqualPremise(ISentence premise);
        public bool Equals(object? obj);
        public int GetHashCode();
    }

    public abstract class Sentence : ISentence {
        public ISentence? Parent { get; set; }
        public List<ISentence> Children { get; } = new();

        public bool IsBinary => Children.Count == 2;
        public bool IsUnary => Children.Count == 1;
        public bool IsNullary => Children.Count == 0;
        public bool IsLiteral => this is IAtomicSentence || 
                                 (this is IComplexSentence { IsNegation: true } complex && complex.Children[0] is IAtomicSentence);
        public bool IsNegation => this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.NEGATION;
        public bool IsImplication => this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.IMPLICATION;
        public abstract void SubstituteTerm(Term term, Term replacement);
        public abstract ISentence Negate();
        public abstract ISentence Clone();
    
        public bool IsNegationOf(ISentence other, bool onlyPredSignature = false) {
            if (IsNegation && !other.IsNegation && Compare(Children[0],other)) return true;
            if (other.IsNegation && !IsNegation && Compare(this,other.Children[0])) return true;
            return false;

            bool Compare(ISentence A, ISentence B) {
                return onlyPredSignature ? A.GetPredicate().EqualSignature(B.GetPredicate()) : A.Equals(B);
            }
        }

        public void AddChild(ISentence sentence) {
            Children.Add(sentence);
            sentence.Parent = this;
        }

        public void InsertChild(int index, ISentence sentence) {
            Children.Insert(index, sentence);
            sentence.Parent = this;
        }

        public void SetParentToParentOf(ISentence? parentOfThis) {
            var grandParent = parentOfThis?.Parent;
            if (grandParent == null) {
                Parent = null;
                return;
            }

            ISentence? found = null;
            foreach (var childInParent in grandParent.Children) {
                if (!childInParent.Equals(parentOfThis)) {
                    continue;
                }

                found = childInParent;
            }

            if (found == null) {
                throw new Exception($"this not found in Parent.Children");
            }

            var index = grandParent.Children.IndexOf(found);
            grandParent.Children.RemoveAt(index);
            grandParent.InsertChild(index, this);
        }

        public bool HasScopeConflict(List<Variable>? boundVariables = null) {
            boundVariables ??= new List<Variable>();

            if (this is IComplexSentence { IsQuantifier: true } complexSentence) {
                var boundVariable = ((Quantifier)complexSentence.Connective).Variable;
                if (boundVariables.Contains(boundVariable)) {
                    return true;
                }

                boundVariables.Add(boundVariable);
            }

            return Children.Any(child => child.HasScopeConflict());
        }

        public bool HasQuantifier() {
            if (this is IComplexSentence { IsQuantifier: true }) return true;
            return Children.Any(child => child.HasQuantifier());
        }

        public bool IsCNF() {
            if (IsLiteral) return true;

            var complexSentence = (IComplexSentence)this;

            if (complexSentence.IsNegation || 
                complexSentence.IsQuantifier ||
                complexSentence.Connective == Connective.LogicSymbol.IMPLICATION || 
                complexSentence.Connective == Connective.LogicSymbol.BICONDITIONAL) {
                return false;
            } 
        
            var eval = complexSentence.Connective == Connective.LogicSymbol.DISJUNCTION ? complexSentence.Children.All(child => child.IsDisjunctionOfLiterals()) : Children.All(child => child.IsCNF());
            return eval;
        }

        public bool IsDisjunctionOfLiterals() {
            if (IsLiteral) return true;
            return this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.DISJUNCTION &&
                   Children.All(child => child.IsDisjunctionOfLiterals());
        }

        public List<ISentence> GetLiterals() {
            var literals = new List<ISentence>();
        
            if (IsLiteral)
            {
                literals.Add(this);
                return literals;
            }
        
            foreach (var child in Children)
            {
                literals.AddRange(child.GetLiterals());    
            }
        
            return literals;
        }

        public IPredicate GetPredicate()
        {
            if(!IsLiteral) throw new Exception("Sentence is not a literal");
            return this switch
            {
                IPredicate predicate => predicate,
                IComplexSentence => (IPredicate)Children[0],
                _ => throw new Exception("Literal has no predicate")
            };
        }
    
        public IProposition GetProposition()
        {
            if(!IsLiteral) throw new Exception("Sentence is not a literal");
            return this switch
            {
                IProposition proposition => proposition,
                IComplexSentence => (IProposition)Children[0],
                _ => throw new Exception("Literal has no proposition")
            };
        }

        public void AddTime(int t) {
            if (this is IAtomicSentence atomicSentence) {
                atomicSentence.Time += t;
            }
            else {
                foreach (var child in Children) {
                    child.AddTime(t);
                }
            }
        }

        public bool IsImplicationAndEqualPremise(ISentence premise) {
            if (!IsImplication) {
                return false;
            }

            var complexSentence = (IComplexSentence)this;
            return complexSentence.Children[0].Equals(premise);
        }

        public bool IsPropositional() {
            var isPropositional = this switch {
                IProposition => true,
                IComplexSentence complexSentence => complexSentence.Children.All(child => child.IsPropositional()),
                _ => false
            };

            if (!isPropositional) {
                Logger.Log($"{this} is not propositional");
            }
        
            return isPropositional;
        }

        // No variables anywhere (distinct from IsPropositional, which is about zero-arity atoms).
        public bool IsGround() {
            if (this is IPredicate predicate) return predicate.GetVariables().Length == 0;
            return Children.All(child => child.IsGround());
        }

        public override bool Equals(object? obj) {
            if (obj == null || GetType() != obj.GetType()) { //TODO: what if we compare atomic pred with complex pred
                return false;
            }
        
            return Children.SequenceEqual(((Sentence)obj).Children);
        }

        public override int GetHashCode() {
            return Children.Aggregate(0, (current, child) => HashCode.Combine(current, child.GetHashCode()));
        }

        public override string ToString() {
            return "Sentence";
        }
    }
}