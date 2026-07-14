using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public interface IPredicate : IAtomicSentence {
        IReadOnlyList<Term> Terms { get; }
        Variable[] GetVariables();
        bool EqualSignature(IPredicate other);
        int Arity { get; }
    }

    public class Predicate : AtomicSentence, IPredicate {
        // Copied in and handed back read-only: Equals/GetHashCode read these, and a caller who
        // kept a reference to the array it passed could otherwise mutate a sentence that a hash
        // set has already bucketed.
        private readonly Term[] _terms;

        public IReadOnlyList<Term> Terms => _terms;
        public int Arity => _terms.Length;
        public bool EqualSignature(IPredicate other) => Symbol == other.Symbol && Arity == other.Arity;

        public Predicate(string predicateSymbol, Term[] terms) : base(predicateSymbol) {
            _terms = (Term[])terms.Clone();
        }

        public override ISentence Substitute(Term target, Term replacement) {
            var terms = new Term[_terms.Length];
            for (var i = 0; i < _terms.Length; i++) {
                terms[i] = _terms[i].Substitute(target, replacement);
            }

            return new Predicate(Symbol, terms);
        }

        public Variable[] GetVariables() {
            var variables = new List<Variable>();
            foreach (var term in _terms) {
                variables.AddRange(term.GetVariables());
            }
            return variables.ToArray();
        }

        protected override bool EqualsCore(Sentence other) {
            if (!base.EqualsCore(other)) {
                return false;
            }

            var predicate = (Predicate)other;

            if (_terms.Length != predicate._terms.Length) {
                return false;
            }

            for (var i = 0; i < _terms.Length; i++) {
                if (!_terms[i].Equals(predicate._terms[i])) {
                    return false;
                }
            }

            return true;
        }

        protected override int ComputeHashCode() {
            var hash = base.ComputeHashCode();
            return _terms.Aggregate(hash, (current, term) => HashCode.Combine(current, term.GetHashCode()));
        }

        public override string ToString() {
            return $"{Symbol}({string.Join<Term>(",", _terms)})";
        }
    }
}
