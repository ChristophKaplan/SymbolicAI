using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    public class AbductiveChainingTests : TestBase {
        private List<List<ISentence>> Explain(string observation, string[] abducibles, params string[] kb) =>
            new AbductiveChaining().Explain(Set(kb), S(observation), abducibles);

        private static List<HashSet<string>> Keys(List<List<ISentence>> explanations) =>
            explanations.Select(e => e.Select(s => s.ToString()!).ToHashSet()).ToList();

        private string K(string sentence) => S(sentence).ToString()!;

        [Test]
        public void SingleFact_Abduced() {
            var explanations = Explain("CanCook(Maria)", new[] { "Role" },
                "Role(z,Female) => CanCook(z)");
            Assert.That(explanations, Has.Count.EqualTo(1));
            Assert.That(explanations[0].Single().ToString(), Is.EqualTo(K("Role(Maria,Female)")));
        }

        [Test]
        public void AlreadyDerivable_EmptyExplanation() {
            var explanations = Explain("Wet", new[] { "Rain" },
                "Rain", "Rain => Wet");
            Assert.That(explanations, Has.Count.EqualTo(1));
            Assert.That(explanations[0], Is.Empty);
        }

        [Test]
        public void AlternativeExplanations_BothFound() {
            var keys = Keys(Explain("Wet", new[] { "Rain", "Sprinkler" },
                "Rain => Wet", "Sprinkler => Wet"));
            Assert.That(keys, Has.Count.EqualTo(2));
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("Rain") })), Is.True);
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("Sprinkler") })), Is.True);
        }

        // A directly contradicted assumption is no explanation.
        [Test]
        public void InconsistentAssumption_Discarded() {
            var keys = Keys(Explain("Wet", new[] { "Rain", "Sprinkler" },
                "NOT Rain", "Rain => Wet", "Sprinkler => Wet"));
            Assert.That(keys, Has.Count.EqualTo(1));
            Assert.That(keys[0].SetEquals(new[] { K("Sprinkler") }), Is.True);
        }

        // An assumption whose consequences contradict the KB is no explanation either.
        [Test]
        public void IndirectlyInconsistentAssumption_Discarded() {
            var explanations = Explain("Wet", new[] { "Rain" },
                "Sunny", "Rain => NOT Sunny", "Rain => Wet");
            Assert.That(explanations, Is.Empty);
        }

        [Test]
        public void ChainedAbduction_AssumesThroughRules() {
            var keys = Keys(Explain("R(a)", new[] { "P", "Q" },
                "P(x) => Q(x)", "Q(x) => R(x)"));
            Assert.That(keys, Has.Count.EqualTo(2));
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("P(a)") })), Is.True);
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("Q(a)") })), Is.True);
        }

        [Test]
        public void ConjunctiveBody_AssumesBothPremises() {
            var keys = Keys(Explain("R(a)", new[] { "P", "Q", "S" },
                "(P(x) AND Q(x)) => R(x)", "S(x) => R(x)"));
            Assert.That(keys, Has.Count.EqualTo(2));
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("P(a)"), K("Q(a)") })), Is.True);
            Assert.That(keys.Any(k => k.SetEquals(new[] { K("S(a)") })), Is.True);
        }

        [Test]
        public void NegativeLiteral_Abducible() {
            var explanations = Explain("NeedsJob(a)", new[] { "Employed" },
                "Subject(a)", "(Subject(x) AND NOT Employed(x)) => NeedsJob(x)");
            Assert.That(explanations, Has.Count.EqualTo(1));
            Assert.That(explanations[0].Single().ToString(), Is.EqualTo(K("NOT Employed(a)")));
        }

        [Test]
        public void NonAbducibleGoal_NoExplanation() {
            var explanations = Explain("Wet", new string[0], "Rain => Wet");
            Assert.That(explanations, Is.Empty);
        }

        // Supersets of another explanation are pruned.
        [Test]
        public void Minimality_SupersetsPruned() {
            var keys = Keys(Explain("Wet", new[] { "Rain", "Storm" },
                "Rain => Wet", "(Storm(x) AND Rain) => Wet"));
            Assert.That(keys, Has.Count.EqualTo(1));
            Assert.That(keys[0].SetEquals(new[] { K("Rain") }), Is.True);
        }
    }
}
