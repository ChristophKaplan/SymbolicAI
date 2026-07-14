using System;
using System.Collections.Generic;

namespace FirstOrderLogic {
    public class Function : Term, IEquatable<Function> {
        // Copied in and handed back read-only: Equals/GetHashCode read these, and a caller who
        // kept a reference to the array it passed could otherwise mutate a term that a hash set
        // has already bucketed.
        private readonly Term[] _terms;

        public IReadOnlyList<Term> Terms => _terms;
        public int Arity => _terms.Length;
        public bool IsConstant => Arity == 0;

        public Function(string termSymbol, Term[] terms) : base(termSymbol) {
            _terms = (Term[])terms.Clone();
        }

        protected Function(string termSymbol) : base(termSymbol) {
            _terms = Array.Empty<Term>();
        }

        public bool Equals(Function? other)
        {
            if (other is null) {
                return false;
            }

            if(!EqualSignature(other)) {
                return false;
            }

            for (var i = 0; i < _terms.Length; i++) {
                if (!_terms[i].Equals(other._terms[i])) {
                    return false;
                }
            }

            return true;
        }

        protected override bool EqualsCore(Term other)
        {
            return other is Function function && Equals(function);
        }

        protected override int ComputeHashCode() {
            var hash = new HashCode();
            hash.Add(TermSymbol);
            foreach (var term in _terms) {
                hash.Add(term);
            }
            return hash.ToHashCode();
        }

        public bool EqualSignature(Function other) => Arity == other.Arity && TermSymbol.Equals(other.TermSymbol);

        public override string ToString() {
            return IsConstant ? base.ToString() : $"{base.ToString()}({string.Join<Term>(",", _terms)})";
        }
    }
}
