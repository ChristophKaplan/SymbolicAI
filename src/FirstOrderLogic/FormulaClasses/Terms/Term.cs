using System;
using System.Collections.Generic;
using LRParser.Language;
using System.Linq;

namespace FirstOrderLogic {
    public abstract class Term : ILanguageObject {
        public readonly string TermSymbol;

        protected Term(string termSymbol) {
            TermSymbol = termSymbol;
        }
    
        // Sealed so that it, and the hash below, stay the single pair of entry points; subclasses
        // refine EqualsCore/ComputeHashCode instead. EqualsCore does its own type test rather than
        // getting an exact-type check here: a Constant is an arity-0 Function and the two must
        // stay equal, whereas a symbol-only comparison would equate Variable("x") with
        // Function("x"). Every subclass must define its own sound equality.
        public sealed override bool Equals(object? obj) {
            return obj is Term other && EqualsCore(other);
        }

        protected abstract bool EqualsCore(Term other);

        // Terms are immutable, so a term's hash is computed once and reused: resolution hashes
        // whole trees on every seen-set probe, and recomputing them recursively dominated it.
        // Zero doubles as "not computed yet" — a term that really hashes to 0 just recomputes.
        // That keeps this a single field, so a racing reader can never see the flag land before
        // the value; the worst a race costs is a repeated, identical computation.
        private int _hashCode;

        public sealed override int GetHashCode() {
            if (_hashCode == 0) {
                _hashCode = ComputeHashCode();
            }

            return _hashCode;
        }

        protected abstract int ComputeHashCode();

        public override string ToString() {
            return TermSymbol;
        }
    
        public Variable[] GetVariables() {
            switch (this) {
                case Variable variable:
                    return new[] { variable };
                case Function function: {
                    var variables = new List<Variable>();
                    foreach (var term in function.Terms) {
                        variables.AddRange(term.GetVariables());
                    }
                    return variables.ToArray();
                }
                default:
                    return Array.Empty<Variable>();
            }
        }
    
        // Returns the same instance when nothing matched, so unchanged subterms are shared.
    public Term Substitute(Term target, Term replacement) {
        if (Equals(target)) {
            return replacement;
        }

        if (this is Function { Arity: > 0 } function) {
            var rebuilt = new Term[function.Terms.Count];
            var changed = false;
            for (var i = 0; i < function.Terms.Count; i++) {
                rebuilt[i] = function.Terms[i].Substitute(target, replacement);
                if (!ReferenceEquals(rebuilt[i], function.Terms[i])) {
                    changed = true;
                }
            }

            if (changed) {
                return new Function(function.TermSymbol, rebuilt);
            }
        }

        return this;
    }

    public bool Occurs(Variable variable) {
            switch (this) {
                case Variable v:
                    return v.Equals(variable);
                case Function function:
                    foreach (var term in function.Terms) {
                        if (term.Occurs(variable)) {
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }
    }
}
