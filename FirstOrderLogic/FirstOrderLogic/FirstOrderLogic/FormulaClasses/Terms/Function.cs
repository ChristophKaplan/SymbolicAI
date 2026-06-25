using System;

namespace FirstOrderLogic {
    public class Function : Term, IEquatable<Function> {
        public readonly Term[] Terms;
        public int Arity => Terms.Length;
        public bool IsConstant => Arity == 0;

        public Function(string termSymbol, Term[] terms) : base(termSymbol) {
            Terms = terms;
        }
        protected Function(string termSymbol) : base(termSymbol) {
            Terms = Array.Empty<Term>();
        }
    
        public Function(Function other) : base(other.TermSymbol) {
            Terms = new Term[other.Terms.Length];
            for (var i = 0; i < other.Terms.Length; i++) {
                Terms[i] = other.Terms[i].Clone();
            }
        }
    
        public bool Equals(Function? other)
        {
            if (other is null) {
                return false;
            }

            if(!EqualSignature(other)) {
                return false;
            }

            for (var i = 0; i < Terms.Length; i++) {
                if (!Terms[i].Equals(other.Terms[i])) {
                    return false;
                }
            }
        
            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is Function other && Equals(other);
        }
    
        public override int GetHashCode() {
            var hash = new HashCode();
            hash.Add(TermSymbol);
            foreach (var term in Terms) {
                hash.Add(term);
            }
            return hash.ToHashCode();
        }
    
        public bool EqualSignature(Function other) => Arity == other.Arity && TermSymbol.Equals(other.TermSymbol);
    
        public override string ToString() {
            return IsConstant ? base.ToString() : $"{base.ToString()}({string.Join<Term>(",", Terms)})";
        }
    }
}