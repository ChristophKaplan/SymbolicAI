using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public interface IPredicate : IAtomicSentence {
        Term[] Terms { get; }
        Variable[] GetVariables();
        bool EqualSignature(IPredicate other);
        int Arity { get; }
    }

    public class Predicate : AtomicSentence, IPredicate {
        public Term[] Terms { get; }
        public int Arity => Terms.Length;
        public bool EqualSignature(IPredicate other) => Symbol == other.Symbol && Arity == other.Arity;

        public Predicate(string predicateSymbol, Term[] terms) : base(predicateSymbol) {
            Terms = terms;
        }

        public override ISentence Substitute(Term target, Term replacement) {
            var terms = new Term[Terms.Length];
            for (var i = 0; i < Terms.Length; i++) {
                terms[i] = Terms[i].Substitute(target, replacement);
            }

            return new Predicate(Symbol, terms);
        }

        public Variable[] GetVariables() {
            var variables = new List<Variable>();
            foreach (var term in Terms) {
                variables.AddRange(term.GetVariables());
            }
            return variables.ToArray();
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
            return $"{Symbol}({string.Join<Term>(",", Terms)})";
        }
    }
}
