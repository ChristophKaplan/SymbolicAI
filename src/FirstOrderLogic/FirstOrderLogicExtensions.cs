using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic {
    public static class FirstOrderLogicExtensions
    {
        public static ISentence ConnectSentences(this List<ISentence> sentences, Connective.LogicSymbol connective = Connective.LogicSymbol.CONJUNCTION) {
            return sentences.Aggregate((a, b) => (ISentence)new ComplexSentence(a, connective, b));
        }
    
        private delegate void TransformationDelegate(ref ISentence sentence);

        public static ISentence ToPrenexForm(this ISentence sentence, out List<ISentence> steps) {
            steps = new List<ISentence>();
            return ToPrenexFormCore(sentence, steps);
        }

        // Trace-free overload: skips the per-step clones that dominate normalisation cost.
        public static ISentence ToPrenexForm(this ISentence sentence) =>
            ToPrenexFormCore(sentence, null);

        private static ISentence ToPrenexFormCore(ISentence sentence, List<ISentence>? steps) {
            var clone = sentence;

            var transformations = new List<TransformationDelegate> {
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.SimplifyConstants, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DissolveBiconditional, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DissolveImplication, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.PushNegation, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DoubleNegation, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.Absorption, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.AssociationAndIdem, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.PullQuantifier, ref s),
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.RemoveDuplicateQuantifier, ref s),
            };

            ApplyUntilStable(ref clone, transformations, steps);
            return clone;
        }

        private static void ApplyUntilStable(
            ref ISentence clone, List<TransformationDelegate> transformations, List<ISentence>? steps) {
            while (true) {
                var start = clone;
                foreach (var transform in transformations) {
                    transform(ref clone);
                    steps?.Add(clone);
                }
                if (start.Equals(clone)) {
                    break;
                }
            }
        }

        public static ISentence ToConjunctiveNormalForm(this ISentence sentence, out List<ISentence> steps) {
            steps = new List<ISentence>();
            return ToConjunctiveNormalFormCore(sentence, steps);
        }

        public static ISentence ToConjunctiveNormalForm(this ISentence sentence) =>
            ToConjunctiveNormalFormCore(sentence, null);

        private static ISentence ToConjunctiveNormalFormCore(ISentence sentence, List<ISentence>? steps) {
            if (sentence.ContainsNaf()) {
                throw new ArgumentException(
                    $"'{sentence}' contains negation-as-failure, which has no classical semantics — CNF/Resolution cannot consume it.");
            }

            var clone = ToPrenexFormCore(sentence, steps);

            var transformations = new List<TransformationDelegate> {
                (ref ISentence s) => TransformationFOL.Transform(TransformationFOL.EquivType.DistributionOfDisjunction, ref s)
            };

            ApplyUntilStable(ref clone, transformations, steps);

            if(!clone.IsCNF()) { throw new Exception("Sentence is not in CNF"); }
            return clone;
        }

        // Process-wide so that independently skolemized sentences never share a witness name:
        // conjoining two results that both contain sk1 would conflate distinct existentials.
        private static int _skolemCounter;

        // Test seam for assertions that pin exact Skolem names.
        public static void ResetSkolemCounter() => System.Threading.Interlocked.Exchange(ref _skolemCounter, 0);

        public static ISentence SkolemForm(this ISentence sentence) {
            var clone = sentence;

            // Each existential becomes a Skolem term over the universals enclosing it ('$' is
            // unparseable, so a witness can never resolve against a user constant). Free
            // variables are implicitly universal at widest scope, so every witness depends on them.
            var prefix = new List<Quantifier>();
            var current = clone;
            while (current is IComplexSentence { IsQuantifier: true } quantified) {
                prefix.Add((Quantifier)quantified.Connective);
                current = quantified.Children[0];
            }

            if (current.HasQuantifier()) {
                throw new ArgumentException(
                    $"'{sentence}' is not in prenex normal form — call ToPrenexForm first.", nameof(sentence));
            }

            // A binder shadowed by an inner same-named one binds nothing (every matrix
            // occurrence belongs to the innermost binder): it neither skolemizes nor may its
            // name appear as a witness argument, where it would capture the inner binder's
            // occurrences. Nothing can depend on a vacuous universal, so dropping it is exact.
            var lastBinderOf = new Dictionary<string, int>();
            for (var i = 0; i < prefix.Count; i++) {
                lastBinderOf[prefix[i].Variable.TermSymbol] = i;
            }

            var substitution = new Dictionary<Variable, Function>();
            var universalsInScope = current.GetLiterals()
                .SelectMany(l => l.VariablesOf())
                .Where(v => !lastBinderOf.ContainsKey(v.TermSymbol))
                .Distinct()
                .ToList();
            for (var i = 0; i < prefix.Count; i++) {
                var quantifier = prefix[i];
                if (lastBinderOf[quantifier.Variable.TermSymbol] != i) {
                    continue;
                }

                if (quantifier.Symbol == Connective.LogicSymbol.UNIVERSAL) {
                    universalsInScope.Add(quantifier.Variable);
                }
                else {
                    var args = universalsInScope
                        .Select(v => (Term)new Variable(v.TermSymbol))
                        .ToArray();
                    substitution.Add(quantifier.Variable,
                        new Function($"sk${System.Threading.Interlocked.Increment(ref _skolemCounter)}", args));
                }
            }

            // Strip the prefix first: substitution is capture-avoiding and stops at a binder of
            // the variable being replaced, so the witnesses only reach the matrix once its
            // variables are free. Shadowed-binder occurrences all belong to the innermost binder
            // (the only one that skolemizes), so substituting after removal is exact.
            TransformationFOL.Transform(TransformationFOL.EquivType.RemoveQuantifier, ref clone);

            foreach (var variable in substitution.Keys) {
                clone = clone.Substitute(variable, substitution[variable]);
            }

            return clone;
        }

        public static List<Clause> GetClauseSet(this ISentence sentence, List<Clause>? clauseSet = null) {
            if (sentence.ContainsNaf()) {
                throw new ArgumentException(
                    $"'{sentence}' contains negation-as-failure, which has no classical semantics — CNF/Resolution cannot consume it.");
            }
            if (!sentence.IsCNF()) { throw new Exception("Sentence is not in CNF"); }
        
            clauseSet ??= new List<Clause>();

            if(sentence.IsDisjunctionOfLiterals())
            {
                var clauseList = sentence.GetLiterals();
                clauseSet.Add(new Clause(clauseList.ToArray()));
                return clauseSet;
            }
        
            foreach (var child in sentence.Children)
            {
                child.GetClauseSet(clauseSet);
            }
        
            return clauseSet;
        }

        public static bool ContainsNaf(this ISentence sentence) =>
            sentence.IsNaf || sentence.Children.Any(ContainsNaf);

        // Positive literals within the list whose negation is also present (its counter is the
        // negation). Facts are implicitly universally quantified, so a conflict is detected by
        // unification after renaming apart: the universal Sunny(x) conflicts with ¬Sunny(a).
        public static List<ISentence> Conflicts(this IReadOnlyList<ISentence> literals) {
            var claims = new List<ISentence>();
            for (var i = 0; i < literals.Count; i++) {
                for (var j = i + 1; j < literals.Count; j++) {
                    if (literals[i].IsNegation == literals[j].IsNegation) {
                        continue;
                    }

                    var positive = literals[i].IsNegation ? literals[j] : literals[i];
                    var negatedAtom = (literals[i].IsNegation ? literals[i] : literals[j]).Children[0];
                    if (!Unificator.TryUnify(positive, RenamedApartFrom(negatedAtom, positive), out _)) {
                        continue;
                    }

                    if (!claims.Contains(positive)) {
                        claims.Add(positive);
                    }
                }
            }
            return claims;
        }

        // "cf$" names cannot pre-exist ('$' is unparseable and no renaming scheme uses this
        // prefix), so a per-call counter suffices.
        private static ISentence RenamedApartFrom(ISentence literal, ISentence other) {
            var taken = other.VariablesOf().Select(v => v.TermSymbol).ToHashSet();
            var fresh = 0;
            return literal.Renamed(v => taken.Contains(v.TermSymbol) ? new Variable($"cf${++fresh}") : null);
        }
    }
}