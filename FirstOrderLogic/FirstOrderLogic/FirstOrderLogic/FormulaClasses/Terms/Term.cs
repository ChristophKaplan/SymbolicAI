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
    
        public Term Clone() {
            return this switch {
                Variable variable => new Variable(variable),
                Function function => new Function(function),
                _ => throw new Exception($"Unknown Term Type: {this}")
            };
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
    
        // Shared by Predicate and Function: replace matching terms in place, recursing into functions.
    internal static void SubstituteAll(Term[] terms, Term term, Term replacement) {
        for (var i = 0; i < terms.Length; i++) {
            var curTerm = terms[i];
            if (curTerm.Equals(term)) {
                terms[i] = replacement;
            } else if (curTerm is Function function) {
                function.SubstituteTerm(term, replacement);
            }
        }
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
