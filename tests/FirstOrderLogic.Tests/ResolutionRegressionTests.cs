using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Behavioural lock for Resolution.Resolve: a broad battery of (KB, goal, expected) cases.
    // This exists to guard redundancy-elimination changes (tautology removal, subsumption,
    // factoring) — every case must keep the same boolean result before and after such changes.
    public class ResolutionRegressionTests : TestBase {
        [TestCase("A AND (A => B)", "B")]
        [TestCase("(A => B) AND (B => C) AND A", "C")]
        [TestCase("(A OR B) AND (NOT A)", "B")]
        [TestCase("(A OR B) AND (NOT A) AND (NOT B) AND C", "Z")]      // contradictory KB entails anything
        [TestCase("A AND (NOT A)", "Z")]                              // direct contradiction
        [TestCase("(A OR B) AND (A OR (NOT B))", "A")]                // requires factoring (A∨A -> A)
        [TestCase("(A OR B) AND ((NOT A) OR B) AND ((NOT B))", "Z")]  // unsat KB
        [TestCase("(A OR B) AND A AND (A => C)", "C")]                // A subsumes (A∨B); proof via A
        [TestCase("Human(Sokrates) AND (Human(x) => Mortal(x))", "Mortal(Sokrates)")]
        [TestCase("(P(x) => Q(x)) AND (Q(x) => R(x)) AND P(a)", "R(a)")]
        [TestCase("P(f(a)) AND (P(x) => Q(x))", "Q(f(a))")]
        [TestCase("Loves(a, b) AND (Loves(x, y) => Knows(x, y))", "Knows(a, b)")]
        public void Resolve_Entailed_True(string kb, string goal) {
            AssertResolves(kb, goal, expected: true);
        }

        [TestCase("A", "B")]
        [TestCase("A => B", "A")]                                      // affirming the consequent
        [TestCase("A OR B", "A")]
        [TestCase("(A OR B) AND A", "B")]                              // subsumption removes (A∨B); still not entailed
        [TestCase("P OR (NOT P)", "Z")]                               // tautology entails nothing contingent
        [TestCase("P(a) OR (NOT P(a))", "Z(a)")]
        [TestCase("P(a)", "Q(b)")]
        [TestCase("P(a) AND (P(x) => Q(x))", "Q(b)")]                  // wrong constant
        [TestCase("Human(Sokrates)", "Mortal(Sokrates)")]
        public void Resolve_NotEntailed_False(string kb, string goal) {
            AssertResolves(kb, goal, expected: false);
        }
    }
}
