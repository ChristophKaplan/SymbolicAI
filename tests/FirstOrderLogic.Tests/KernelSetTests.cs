using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Kernel set = minimal subset of B that entails α (Hansson 1994).
    public class KernelSetTests : TestBase {
        private readonly KernelSets _ks = new();

        [Test]
        public void FindKernel_DirectEntailment() {
            var kernel = _ks.FindKernel(Set("P(a)", "Q(b)"), S("P(a)"));
            Assert.That(kernel, Is.Not.Null);
            Assert.That(kernel!.Count, Is.EqualTo(1));
            Assert.That(kernel[0].ToString(), Is.EqualTo("P(a)"));
        }

        [Test]
        public void FindKernel_DropsIrrelevant() {
            var kernel = _ks.FindKernel(Set("P(a)", "P(x) => Q(x)", "R(b)"), S("Q(a)"));
            Assert.That(kernel, Is.Not.Null);
            Assert.That(kernel!.Count, Is.EqualTo(2));
            Assert.That(kernel.Any(s => s.ToString() == "R(b)"), Is.False);
        }

        [Test]
        public void FindKernel_NotEntailed_ReturnsNull() {
            Assert.That(_ks.FindKernel(Set("P(a)"), S("Q(b)")), Is.Null);
        }

        [Test]
        public void FindAllKernels_TwoIndependentProofs() {
            var kernels = _ks.FindAllKernels(
                Set("P(a)", "P(x) => S(x)", "R(a)", "R(x) => S(x)"), S("S(a)"));
            Assert.That(kernels.Count, Is.EqualTo(2));
            Assert.That(kernels.All(k => k.Count == 2), Is.True);
        }

        [Test]
        public void FindAllKernels_NotEntailed_Empty() {
            var kernels = _ks.FindAllKernels(Set("P(a)", "Q(b)"), S("R(c)"));
            Assert.That(kernels, Is.Empty);
        }

        [Test]
        public void FindAllKernels_ThreeIndependentProofs() {
            var kernels = _ks.FindAllKernels(
                Set("P(a)", "P(x) => S(x)", "R(a)", "R(x) => S(x)", "T(a)", "T(x) => S(x)"),
                S("S(a)"));
            Assert.That(kernels.Count, Is.EqualTo(3));
            Assert.That(kernels.All(k => k.Count == 2), Is.True);
        }

        [Test]
        public void FindAllKernels_IsDeterministicAcrossCalls() {
            var b = Set("P(a)", "P(x) => Q(x)", "Q(x) => S(x)", "R(b)");
            var first = _ks.FindAllKernels(b, S("S(a)"));
            var second = _ks.FindAllKernels(b, S("S(a)"));
            Assert.That(second.Count, Is.EqualTo(first.Count));
            Assert.That(Keys(second), Is.EqualTo(Keys(first)));
        }

        private static List<string> Keys(List<List<ISentence>> kernels) =>
            kernels.Select(k => string.Join("|", k.Select(s => s.ToString()).OrderBy(s => s)))
                   .OrderBy(s => s).ToList();

        [Test]
        public void SameInstance_DifferentBeliefBases_AreIndependent() {
            var entailed = _ks.FindAllKernels(Set("P(a)", "P(x) => S(x)"), S("S(a)"));
            Assert.That(entailed.Count, Is.EqualTo(1));

            var notEntailed = _ks.FindAllKernels(Set("R(a)"), S("S(a)"));
            Assert.That(notEntailed, Is.Empty);

            var viaOtherPath = _ks.FindAllKernels(Set("R(a)", "R(x) => S(x)"), S("S(a)"));
            Assert.That(viaOtherPath.Count, Is.EqualTo(1));

            Assert.That(_ks.FindKernel(Set("R(a)"), S("S(a)")), Is.Null);
            Assert.That(_ks.FindKernel(Set("P(a)", "P(x) => S(x)"), S("S(a)")), Is.Not.Null);
        }

        [Test]
        public void FindAllKernels_KernelsAreEntailingAndMinimal() {
            var b = Set("P(a)", "P(x) => Q(x)", "Q(x) => S(x)");
            var kernels = _ks.FindAllKernels(b, S("S(a)"));
            Assert.That(kernels.Count, Is.GreaterThanOrEqualTo(1));
            foreach (var kernel in kernels) {
                Assert.That(new Theory(kernel).Entails(S("S(a)")), Is.True);
                foreach (var s in kernel) {
                    var smaller = kernel.Where(x => !ReferenceEquals(x, s)).ToList();
                    Assert.That(new Theory(smaller).Entails(S("S(a)")), Is.False);
                }
            }
        }
    }
}
