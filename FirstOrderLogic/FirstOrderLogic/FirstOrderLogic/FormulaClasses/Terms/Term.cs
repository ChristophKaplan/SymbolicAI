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
    
        public override bool Equals(object? obj) {
            if(ReferenceEquals(this, obj)) {
                return true;
            }
        
            if (obj is not Term other) {
                return false;
            }
            return TermSymbol.Equals(other.TermSymbol);
        }
    
        public override int GetHashCode() {
            return TermSymbol.GetHashCode();
        }

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
    
        // Returns a new term with every occurrence of `target` replaced; returns the same
    // instance (by reference) when nothing matched, so unchanged subterms are shared.
    public Term Substitute(Term target, Term replacement) {
        if (Equals(target)) {
            return replacement;
        }

        if (this is Function { Arity: > 0 } function) {
            var rebuilt = new Term[function.Terms.Length];
            var changed = false;
            for (var i = 0; i < function.Terms.Length; i++) {
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
