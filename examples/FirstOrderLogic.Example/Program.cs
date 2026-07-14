using System;
using FirstOrderLogic;

namespace FirstOrderLogicExample {
    class Program {
        static void Main(string[] args) {
            var logic = new FirstOrderLogic.FirstOrderLogic();

            var sentence = (ISentence)logic.TryParse("(Human(Sokrates) AND (FORALL x (Human(x) => Mortal(x))))");
            var prenexForm = sentence.ToPrenexForm(out var steps);
            var skolemForm = prenexForm.SkolemForm();
            var consequence = (ISentence)logic.TryParse("Mortal(Sokrates)");

            var result = Resolution.Resolve(skolemForm, consequence);

            Console.WriteLine(result);
        }
    }
}