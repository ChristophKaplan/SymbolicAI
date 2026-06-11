using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public interface IPredicate : IAtomicSentence {
        Term[] Terms { get; }
        Variable[] GetVariables();
        bool HasBoundVariables();
        bool EqualSignature(IPredicate other);
        int Arity { get; }
    }

    public class Predicate : AtomicSentence, IPredicate {
        public Term[] Terms { get; }
        public int Arity => Terms.Length;
        public override ISentence Clone() => new Predicate(this);
        public bool EqualSignature(IPredicate other) => Symbol == other.Symbol && Arity == other.Arity;

        public Predicate(string predicateSymbol, Term[] terms) : base(predicateSymbol) {
            Terms = terms;
        }
    
        public Predicate(string predicateSymbol, Term[] terms, int time) : base(predicateSymbol, time) {
            Terms = terms;
        }

        private Predicate(IPredicate other) : base(other) {
            Terms = new Term[other.Terms.Length];
            for (int i = 0; i < other.Terms.Length; i++) {
                Terms[i] = other.Terms[i].Clone();
            }
        }

        public override void SubstituteTerm(Term term, Term replacement) {
            var terms = Terms;
            var length = terms.Length;
            for (var i = 0; i < length; i++) {
                var curTerm = terms[i];
                if (curTerm.Equals(term)) {
                    terms[i] = replacement;
                } else if (curTerm is Function function) {
                    function.SubstituteTerm(term, replacement);
                }
            }
        }
    
        public Variable[] GetVariables() {
            var variables = new List<Variable>();
            foreach (var term in Terms) {
                variables.AddRange(term.GetVariables());
            }
            return variables.ToArray();
        }

        public bool HasBoundVariables() {
            ISentence current = this;
            while (current.Parent != null) {
                current = current.Parent;
            
                if(current is IComplexSentence { IsQuantifier: true }) {
                    return true;
                }
            }
        
            return false;
        }

        public override bool Equals(object? obj) {
            if (!base.Equals(obj)) {
                return false;
            }
        
            var other = (Predicate)obj;
        
            if (Terms.Length != other.Terms.Length) {
                return false;
            }
        
            for (var i = 0; i < Terms.Length; i++) {
                if (!Terms[i].Equals(other.Terms[i])) {
                    return false;
                }
            }
        
            return true;
        }
    
        public override int GetHashCode() {
            var hash = base.GetHashCode();
            return Terms.Aggregate(hash, (current, term) => HashCode.Combine(current, term.GetHashCode()));
        }

        public override string ToString() {
            return $"{Symbol}({string.Join<Term>(",", Terms)}){(Time.HasValue ? $"^{Time}" : "")}";
        }
    }
}
