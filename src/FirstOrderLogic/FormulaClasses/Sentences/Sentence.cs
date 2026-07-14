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
            if (IsNegation && !other.IsNegation && Compare(Children[0],other))
            {
                return true;
            }

            if (other.IsNegation && !IsNegation && Compare(this,other.Children[0]))
            {
                return true;
            }

            return false;

            // A non-atomic comparand has no predicate signature to compare, so it simply is not
            // the negation of anything — asking must answer false, not throw.
            bool Compare(ISentence A, ISentence B) {
                if (!onlyPredSignature)
                {
                    return A.Equals(B);
                }

                if (A is IProposition propA)
                {
                    return B is IProposition propB && propA.Symbol.Equals(propB.Symbol);
                }

                if (A is IPredicate predA)
                {
                    return B is IPredicate predB && predA.EqualSignature(predB);
                }

                return false;
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
            if (this is IComplexSentence { IsQuantifier: true })
            {
                return true;
            }

            return Children.Any(child => child.HasQuantifier());
        }

        public bool IsCNF() {
            if (IsLiteral)
            {
                return true;
            }

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
            if (IsLiteral)
            {
                return true;
            }

            return this is IComplexSentence complex && complex.Connective == Connective.LogicSymbol.DISJUNCTION &&
                   Children.All(child => child.IsDisjunctionOfLiterals());
        }

        // Only AND/OR over literals has a literal list: recursing through a negation of a complex
        // child (or a NAF node) would hand back its atoms stripped of the polarity that gives
        // them their meaning, so those inputs are rejected rather than silently mis-answered.
        public List<ISentence> GetLiterals() {
            var literals = new List<ISentence>();
            Collect(this, literals);
            return literals;

            static void Collect(ISentence sentence, List<ISentence> literals) {
                if (sentence.IsLiteral) {
                    literals.Add(sentence);
                    return;
                }

                if (sentence is not IComplexSentence { IsConjunction: true } and
                    not IComplexSentence { IsDisjunction: true }) {
                    throw new InvalidOperationException(
                        $"'{sentence}' is not a conjunction/disjunction of literals, so it has no literal list.");
                }

                foreach (var child in sentence.Children) {
                    Collect(child, literals);
                }
            }
        }

        public IPredicate GetPredicate() => GetAtom<IPredicate>();

        public IProposition GetProposition() => GetAtom<IProposition>();

        private T GetAtom<T>()
        {
            if(!IsLiteral)
            {
                throw new InvalidOperationException($"'{this}' is not a literal");
            }

            return this switch
            {
                T atom => atom,
                IComplexSentence => (T)Children[0],
                _ => throw new InvalidOperationException($"Literal has no {typeof(T).Name}")
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
            if (this is IPredicate predicate)
            {
                return predicate.GetVariables().Length == 0;
            }

            return Children.All(child => child.IsGround());
        }

        // Exact-type check is sound for atomic-vs-complex: an atom and a complex sentence can
        // never be structurally equal. Sealed so that it, and the hash below, stay the single
        // pair of entry points; subclasses refine EqualsCore/ComputeHashCode instead and may
        // therefore cast `other` to their own type.
        public sealed override bool Equals(object? obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            return EqualsCore((Sentence)obj);
        }

        protected virtual bool EqualsCore(Sentence other) {
            return Children.SequenceEqual(other.Children);
        }

        // The AST is immutable, so a node's hash is computed once and reused: resolution hashes
        // whole trees on every seen-set probe, and recomputing them recursively dominated it.
        // Zero doubles as "not computed yet" — a node that really hashes to 0 just recomputes.
        // That keeps this a single field, so a racing reader can never see the flag land before
        // the value; the worst a race costs is a repeated, identical computation.
        private int _hashCode;

        public sealed override int GetHashCode() {
            if (_hashCode == 0) {
                _hashCode = ComputeHashCode();
            }

            return _hashCode;
        }

        protected virtual int ComputeHashCode() {
            return Children.Aggregate(0, (current, child) => HashCode.Combine(current, child.GetHashCode()));
        }

        public override string ToString() {
            return "Sentence";
        }
    }
}