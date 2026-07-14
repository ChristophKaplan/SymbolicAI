using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FirstOrderLogic
{
    public class Unificator
    {
        private readonly int _hashcode;
        public readonly Dictionary<Variable, Term> Substitutions = new();
        public readonly bool IsUnifiable;
        public bool IsEmpty => Substitutions.Count == 0;
        
        public override bool Equals(object? obj) {
            if (ReferenceEquals(this, obj)) {
                return true;
            }
            
            if (obj is not Unificator unificator)
            {
                return false;
            }

            if (unificator.Substitutions.Count != Substitutions.Count)
            {
                return false;
            }

            if (unificator.GetHashCode() != GetHashCode()) {
                return false;
            }
            
            foreach (var (key, value) in Substitutions) {
                if (!unificator.Substitutions.TryGetValue(key, out var otherValue) || !value.Equals(otherValue)) return false;
            }
            
            return true;
        }
        
        public override int GetHashCode() {
            return _hashcode;
        }
        
        // Order-insensitive (XOR of pair hashes): content-equal unifiers built in different
        // binding orders must hash alike, or Equals' hash shortcut splits them.
        private int CalcHashCode() {
            var hash = Substitutions.Count;
            foreach (var (key, value) in Substitutions)
            {
                hash ^= HashCode.Combine(key, value);
            }
            return hash;
        }

        public Unificator(Dictionary<Variable, Term> substitutions)
        {
            if (substitutions.Count == 0)
            {
                throw new Exception("Unificator: missing substitutions");
            }
            
            Substitutions = substitutions;
            IsUnifiable = true;
            _hashcode = CalcHashCode();
        }
        
        public Unificator(ISentence s1, ISentence s2)
        {
            IsUnifiable = UnifyLiteral(s1, s2);
            _hashcode = CalcHashCode();
        }

        // Try-pattern over the constructor: the most general unifier of two literals, or false.
        public static bool TryUnify(ISentence a, ISentence b, out Dictionary<Variable, Term> mgu)
        {
            var unificator = new Unificator(a, b);
            if (!unificator.IsUnifiable)
            {
                mgu = new Dictionary<Variable, Term>();
                return false;
            }

            mgu = new Dictionary<Variable, Term>(unificator.Substitutions);
            return true;
        }

        // The shared single-literal matching primitive: two literals match iff they have the same
        // polarity and their atoms unify, in which case the unifier is returned. Forward/backward
        // chaining and the planner's operator graph all bottom out here, so "what it means for two
        // literals to match" — including the polarity rule — lives in exactly one place.
        public static bool TryMatch(ISentence a, ISentence b, [NotNullWhen(true)] out Unificator? match)
        {
            if (a.IsNegation != b.IsNegation)
            {
                match = null;
                return false;
            }

            var unificator = new Unificator(a, b);
            if (!unificator.IsUnifiable)
            {
                match = null;
                return false;
            }

            match = unificator;
            return true;
        }

        private bool UnifyLiteral(ISentence lit1, ISentence lit2)
        {
            if (!lit1.IsLiteral || !lit2.IsLiteral)
            {
                throw new Exception("Both sentences must be literals");
            }

            var atom1 = GetAtom(lit1);
            var atom2 = GetAtom(lit2);

            if (atom1 is IPredicate pred1 && atom2 is IPredicate pred2)
            {
                if (pred1.Symbol != pred2.Symbol || pred1.Arity != pred2.Arity)
                {
                    return false;
                }

                for (var i = 0; i < pred1.Arity; i++)
                {
                    if (!UnifyTerm(pred1.Terms[i], pred2.Terms[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Propositional atoms carry no terms: they unify iff they are the same atom.
            if (atom1 is IProposition && atom2 is IProposition)
            {
                return atom1.Symbol == atom2.Symbol;
            }

            return false;
        }

        private static IAtomicSentence GetAtom(ISentence literal) => literal switch
        {
            IAtomicSentence atom => atom,
            IComplexSentence complex => (IAtomicSentence)complex.Children[0],
            _ => throw new Exception($"{literal} is not a literal")
        };

        private bool UnifyTerm(Term term1, Term term2)
        {
            if (term1.Equals(term2))
            {
                return true;
            }

            if (term1 is Variable var1)
            {
                return UnifyVar(var1, term2);
            }

            if (term2 is Variable var2)
            {
                return UnifyVar(var2, term1);
            }

            if (term1 is Function func1 && term2 is Function func2)
            {
                return UnifyFunction(func1, func2);
            }

            return false;
        }

        private bool UnifyFunction(Function func1, Function func2)
        {
            if (!func1.EqualSignature(func2))
            {
                return false;
            }

            for (var i = 0; i < func1.Terms.Length; i++)
            {
                if (!UnifyTerm(func1.Terms[i], func2.Terms[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool UnifyVar(Variable var, Term term)
        {
            if (Substitutions.TryGetValue(var, out var subVar))
            {
                return UnifyTerm(subVar, term);
            }

            if (term is Variable termVar && Substitutions.TryGetValue(termVar, out var subTerm))
            {
                return UnifyTerm(var, subTerm);
            }

            if (OccursAfterWalk(var, term))
            {
                return false;
            }

            Substitutions.Add(var, term);
            return true;
        }

        // Occurs check modulo the current substitution: a variable bound to f(x) transitively
        // contains x, so a raw syntactic check would accept cyclic "unifiers" like
        // {x/f(y), y/f(x)} for P(x,f(x)) ~ P(f(y),y). Deliberately not
        // Substitution.Walk(term).Occurs(var): Walk rebuilds function terms, and this runs
        // per variable binding in the unification hot path — the direct recursion is
        // allocation-free.
        private bool OccursAfterWalk(Variable var, Term term)
        {
            switch (term)
            {
                case Variable v:
                    if (v.Equals(var))
                    {
                        return true;
                    }
                    return Substitutions.TryGetValue(v, out var bound) && OccursAfterWalk(var, bound);
                case Function f:
                    foreach (var arg in f.Terms)
                    {
                        if (OccursAfterWalk(var, arg))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        public override string ToString()
        {
            if (Substitutions.Count == 0)
            {
                return $"No substitutions ,IsUnifiable: {IsUnifiable}";
            }

            var sb = new StringBuilder();
            foreach (var (variable, term) in Substitutions)
            {
                sb.Append($"[{variable}/{term}], ");
            }

            if (sb.Length > 0)
            {
                sb.Length -= 2;
            }

            return sb.ToString();
        }

        public void Substitute(Clause clause)
        {
            for (var i = 0; i < clause.Literals.Count; i++)
            {
                clause.Literals[i] = Apply(clause.Literals[i]);
            }
        }

        // Applies the unifier, returning a new sentence; the input is never mutated.
        // Delegates to Substitution so triangular bindings like {x/a, z/f(x)} resolve their
        // chains (z -> f(a)) instead of depending on dictionary iteration order.
        public ISentence Apply(ISentence sentence)
        {
            if (!IsUnifiable)
            {
                throw new Exception("Unificator is not usable!");
            }

            return new Substitution(Substitutions).Apply(sentence);
        }
    }
}