using System;
using System.Collections.Generic;
using System.Linq;
using LRParser.Language;

namespace FirstOrderLogic {
    public interface ISentence : ILanguageObject {
        IReadOnlyList<ISentence> Children { get; }
        bool IsBinary { get; }
        bool IsLiteral { get; }
        bool IsNegation { get; }
        bool IsNaf { get; }
        bool IsImplication { get; }
        bool IsNegationOf(ISentence other, bool onlyPredSignature = false);
        ISentence Substitute(Term target, Term replacement);
        ISentence WithChildren(IReadOnlyList<ISentence> children);
        ISentence Negated();
        bool HasScopeConflict(List<Variable>? boundVariables = null);
        bool HasQuantifier();
        bool IsCNF();
        bool IsPropositional();
        bool IsGround();
        bool IsDisjunctionOfLiterals();
        List<ISentence> GetLiterals();
        IPredicate GetPredicate();
        IProposition GetProposition();
    }

    public abstract class Sentence : ISentence {
        public IReadOnlyList<ISentence> Children { get; protected set; } = Array.Empty<ISentence>();

        public bool IsBinary => Children.Count == 2;
        public bool IsLiteral => this is IAtomicSentence || 
                                 (this is IComplexSentence { IsNegation: true } complex && complex.Children[0] is IAtomicSentence);
        public bool IsNegation => this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.NEGATION;
        public bool IsNaf => this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.NAF;
        public bool IsImplication => this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.IMPLICATION;
        public abstract ISentence Substitute(Term target, Term replacement);
        public abstract ISentence WithChildren(IReadOnlyList<ISentence> children);
        public abstract ISentence Negated();
    
        public bool IsNegationOf(ISentence other, bool onlyPredSignature = false) {
            if (IsNegation && !other.IsNegation && Compare(Children[0],other)) return true;
            if (other.IsNegation && !IsNegation && Compare(this,other.Children[0])) return true;
            return false;

            bool Compare(ISentence A, ISentence B) {
                if (!onlyPredSignature) return A.Equals(B);
                if (A is IProposition propA) return B is IProposition propB && propA.Symbol.Equals(propB.Symbol);
                if (B is IProposition) return false;
                return A.GetPredicate().EqualSignature(B.GetPredicate());
            }
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

            // Each child sees the bindings of its ancestors (including this node's), but not
            // those of its siblings' subtrees: truncate the shared list back after each child
            // instead of copying it per branch.
            var mark = boundVariables.Count;
            foreach (var child in Children) {
                if (child.HasScopeConflict(boundVariables)) {
                    return true;
                }
                boundVariables.RemoveRange(mark, boundVariables.Count - mark);
            }

            return false;
        }

        public bool HasQuantifier() {
            if (this is IComplexSentence { IsQuantifier: true }) return true;
            return Children.Any(child => child.HasQuantifier());
        }

        public bool IsCNF() {
            if (IsLiteral) return true;

            var complexSentence = (IComplexSentence)this;

            if (complexSentence.IsNegation ||
                complexSentence.IsNaf ||
                complexSentence.IsQuantifier ||
                complexSentence.Connective == Connective.LogicSymbol.IMPLICATION ||
                complexSentence.Connective == Connective.LogicSymbol.BICONDITIONAL) {
                return false;
            }

            return complexSentence.IsDisjunction
                ? IsDisjunctionOfLiterals()
                : Children.All(child => child.IsCNF());
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

        public IPredicate GetPredicate() => GetAtom<IPredicate>();

        public IProposition GetProposition() => GetAtom<IProposition>();

        private T GetAtom<T>()
        {
            if(!IsLiteral) throw new Exception("Sentence is not a literal");
            return this switch
            {
                T atom => atom,
                IComplexSentence => (T)Children[0],
                _ => throw new Exception($"Literal has no {typeof(T).Name}")
            };
        }

        public bool IsPropositional() {
            return this switch {
                IProposition => true,
                IComplexSentence complexSentence => complexSentence.Children.All(child => child.IsPropositional()),
                _ => false
            };
        }

        public bool IsGround() {
            if (this is IPredicate predicate) return predicate.GetVariables().Length == 0;
            return Children.All(child => child.IsGround());
        }

        public override bool Equals(object? obj) {
            // Exact-type check is sound for atomic-vs-complex: an atom and a complex sentence can
            // never be structurally equal, and every subclass (AtomicSentence, Predicate,
            // ComplexSentence) starts its own Equals with the same GetType() comparison.
            if (obj == null || GetType() != obj.GetType()) {
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