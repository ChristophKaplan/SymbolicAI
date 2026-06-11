using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Kernel set = minimal subset of B that entails α (Hansson 1994).
    public class KernelSetTests : TestBase {
        private readonly KernelSets _ks = new();

        // α ∈ B trivially entails α — the kernel is {α}.
        [Test]
        public void FindKernel_DirectEntailment() {
            var kernel = _ks.FindKernel(Set("P(a)", "Q(b)"), S("P(a)"));
            Assert.That(kernel, Is.Not.Null);
            Assert.That(kernel!.Count, Is.EqualTo(1));
            Assert.That(kernel[0].ToString(), Is.EqualTo("P(a)"));
        }

        // Modus ponens: {P(a), P(x)=>Q(x)} entails Q(a); the irrelevant R(b) is dropped.
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

        // Two independent proof paths for S(a) yield two kernels.
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

        // Three independent proof paths for S(a) yield three kernels.
        [Test]
        public void FindAllKernels_ThreeIndependentProofs() {
            var kernels = _ks.FindAllKernels(
                Set("P(a)", "P(x) => S(x)", "R(a)", "R(x) => S(x)", "T(a)", "T(x) => S(x)"),
                S("S(a)"));
            Assert.That(kernels.Count, Is.EqualTo(3));
            Assert.That(kernels.All(k => k.Count == 2), Is.True);
        }

        // Caching must be a pure memo: repeated calls give identical results.
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

        // Reusing the same KernelSets instance with *different* belief bases must yield independent,
        // correct results — i.e. a query carries no state over from a previous one.
        [Test]
        public void SameInstance_DifferentBeliefBases_AreIndependent() {
            var entailed = _ks.FindAllKernels(Set("P(a)", "P(x) => S(x)"), S("S(a)"));
            Assert.That(entailed.Count, Is.EqualTo(1));

            // Same instance, a base that no longer entails S(a) — must NOT reuse the earlier result.
            var notEntailed = _ks.FindAllKernels(Set("R(a)"), S("S(a)"));
            Assert.That(notEntailed, Is.Empty);

            // Same instance again, entailed via a different path — must recompute correctly.
            var viaOtherPath = _ks.FindAllKernels(Set("R(a)", "R(x) => S(x)"), S("S(a)"));
            Assert.That(viaOtherPath.Count, Is.EqualTo(1));

            // And FindKernel shares the property.
            Assert.That(_ks.FindKernel(Set("R(a)"), S("S(a)")), Is.Null);
            Assert.That(_ks.FindKernel(Set("P(a)", "P(x) => S(x)"), S("S(a)")), Is.Not.Null);
        }

        // Every kernel must actually entail the target and be subset-minimal.
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
