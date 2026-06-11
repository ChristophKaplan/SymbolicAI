using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstOrderLogic
{
    // A first-order signature (vocabulary / "similarity type"): the non-logical symbols a language is
    // built from — predicate symbols with their arity, plus constant/function symbols. Pure syntax: it
    // declares what *can* be said, not what is true (that is an Interpretation) nor what follows from
    // it (that is a Theory). Constants are arity-0 functions.
    public sealed class Signature
    {
        // A non-logical symbol as it appears in a signature: a name together with its arity, declared
        // once before being applied to any terms. Covers both kinds the signature distinguishes — a
        // predicate symbol or a function symbol (a constant is an arity-0 function symbol); which one it
        // is, is fixed by where it is declared (Builder.Predicate vs. Function/Constant), exactly as the
        // signature keeps predicates and functions in separate namespaces. Contrast Predicate (an
        // IPredicate), which is an *applied* atom carrying actual Terms — there arity is Terms.Length.
        // Arity is an intrinsic property of the symbol, so it travels with the name.
        //
        // It converts implicitly to its name, so a declared symbol drops unchanged into anywhere a string
        // symbol is expected (dictionary keys, comparisons, parser text). Of(args) renders the applied
        // concrete syntax "Name(a, b)" — identical to Predicate.ToString — with an arity check; an
        // arity-0 symbol (a constant) renders as the bare name.
        public sealed class Symbol
        {
            public string Name { get; }
            public int Arity { get; }

            public Symbol(string name, int arity)
            {
                Name = name;
                Arity = arity;
            }

            public static implicit operator string(Symbol symbol) => symbol.Name;
            public override string ToString() => Name;

            // The concrete-syntax application of this symbol to `args`, e.g. Owns.Of("z", "y") => "Owns(z, y)".
            // Throws if the argument count does not match the declared arity.
            public string Of(params string[] args)
            {
                if (args.Length != Arity)
                    throw new ArgumentException(
                        $"{Name}/{Arity} cannot be applied to {args.Length} argument(s).");
                return args.Length == 0 ? Name : $"{Name}({string.Join(", ", args)})";
            }
        }

        private readonly Dictionary<string, int> _predicates;
        private readonly Dictionary<string, int> _functions;

        public Signature(
            IDictionary<string, int>? predicates = null,
            IDictionary<string, int>? functions = null)
        {
            _predicates = predicates != null
                ? new Dictionary<string, int>(predicates)
                : new Dictionary<string, int>();
            _functions = functions != null
                ? new Dictionary<string, int>(functions)
                : new Dictionary<string, int>();
        }

        public IReadOnlyDictionary<string, int> Predicates => _predicates;
        public IReadOnlyDictionary<string, int> Functions  => _functions;

        // The constant symbols (arity-0 functions).
        public IEnumerable<string> Constants => _functions.Where(kv => kv.Value == 0).Select(kv => kv.Key);

        public bool HasPredicate(string symbol) => _predicates.ContainsKey(symbol);
        public bool HasPredicate(string symbol, int arity) =>
            _predicates.TryGetValue(symbol, out var a) && a == arity;

        public bool HasFunction(string symbol) => _functions.ContainsKey(symbol);
        public bool HasConstant(string symbol) =>
            _functions.TryGetValue(symbol, out var a) && a == 0;

        // The predicate symbols (rendered "Symbol/Arity") that occur in `sentence` but are not declared
        // in this signature with a matching arity. Empty ⇒ the sentence is well-formed over this
        // signature. Propositional atoms (no predicate) are ignored.
        public List<string> UndeclaredPredicates(ISentence sentence)
        {
            var missing = new List<string>();
            Collect(sentence, missing);
            return missing;
        }

        public bool Covers(ISentence sentence) => UndeclaredPredicates(sentence).Count == 0;

        private void Collect(ISentence sentence, List<string> missing)
        {
            if (sentence == null) return;

            if (sentence.IsLiteral)
            {
                IPredicate? predicate = null;
                try { predicate = sentence.GetPredicate(); }
                catch { predicate = null; } // propositional atom — no predicate symbol
                if (predicate != null && !HasPredicate(predicate.Symbol, predicate.Arity))
                    missing.Add($"{predicate.Symbol}/{predicate.Arity}");
                return;
            }

            foreach (var child in sentence.Children)
                Collect(child, missing);
        }

        // Fluent construction: sig = new Signature.Builder().Predicate("Role", 2).Constant("Money").Build();
        public sealed class Builder
        {
            private readonly Dictionary<string, int> _predicates = new();
            private readonly Dictionary<string, int> _functions  = new();

            public Builder Predicate(string symbol, int arity)
            {
                _predicates[symbol] = arity;
                return this;
            }

            // Declare a predicate from a Symbol — name and arity travel together.
            public Builder Predicate(Symbol symbol) => Predicate(symbol.Name, symbol.Arity);

            public Builder Constant(string symbol)
            {
                _functions[symbol] = 0;
                return this;
            }

            // Declare a constant from a Symbol (arity-0 function symbol).
            public Builder Constant(Symbol symbol) => Constant(symbol.Name);

            public Builder Function(string symbol, int arity)
            {
                _functions[symbol] = arity;
                return this;
            }

            // Declare a function from a Symbol — name and arity travel together.
            public Builder Function(Symbol symbol) => Function(symbol.Name, symbol.Arity);

            public Signature Build() => new Signature(_predicates, _functions);
        }
    }
}
