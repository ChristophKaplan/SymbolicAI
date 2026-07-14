using System.Collections.Generic;
using System.Linq;
using AIPlanning.Planning.GraphPlan;
using FirstOrderLogic;

namespace AIPlanningTests {
    [TestFixture]
    public class GpLayerTests : PlanningTestBase {
        private static GpLayer LayerWithBelief(params string[] literals) {
            var layer = new GpLayer(0);
            foreach (var literal in literals) {
                layer.TryAdd(new GpLiteralNode(L(literal)));
            }
            return layer;
        }

        [Test]
        public void GetUsableActions_MergesLiteralAnchoredAndPreconditionlessActions() {
            var initialState = Factory.StringToSentence(new() { "P(Obj)" });
            var goals = Factory.StringToSentence(new() { "G(Obj)", "S(Obj)" });
            var act = Factory.Create("Act", new() { "P(Obj)" }, new() { "G(Obj)" });
            var spawn = Factory.Create("Spawn", new List<string>(), new() { "S(Obj)" });
            var operatorGraph = new OperatorGraph(new GpProblem(initialState, goals, new() { act, spawn }));

            var layer = LayerWithBelief("P(Obj)");
            var usable = layer.GetUsableActions(operatorGraph);

            var names = usable.Select(a => a.Signifier).ToList();
            Assert.That(names, Does.Contain("Act"),
                "Act is anchored at the literal node P(Obj) held by the belief state");
            Assert.That(names, Does.Contain("Spawn"),
                "precondition-less actions hang off no literal node and must still be usable");
            Assert.That(names, Does.Not.Contain("Start"));
            Assert.That(names, Does.Not.Contain("Finish"));
        }

        [Test]
        public void ExpandActions_OnlyAppliesActionsWhosePreconditionsHold() {
            var applicable = new GpAction("Applicable", new() { L("P(Obj)") }, new() { L("G(Obj)") });
            var notApplicable = new GpAction("NotApplicable", new() { L("Q(Obj)") }, new() { L("H(Obj)") });

            var layer = LayerWithBelief("P(Obj)");
            layer.ExpandActions(new List<GpAction> { applicable, notApplicable },
                new Dictionary<ISentence, GpAction>());

            var realActions = layer.ActionSet.GetActions(ignorePersistence: true);
            Assert.That(realActions.Select(a => a.Signifier), Is.EqualTo(new[] { "Applicable" }),
                "an action whose precondition Q(Obj) is absent from the belief state must not fire");
        }

        [Test]
        public void ExpandActions_AddsOnePersistActionPerLiteral() {
            var layer = LayerWithBelief("P(Obj)", "R(Obj)");
            layer.ExpandActions(new List<GpAction>(), new Dictionary<ISentence, GpAction>());

            var persistNodes = layer.ActionSet.GetActionNodes
                .Where(node => node.IsPersistenceAction)
                .ToList();
            Assert.That(persistNodes, Has.Count.EqualTo(2),
                "every literal gets exactly one Persist action");

            foreach (var node in persistNodes) {
                Assert.That(node.GpAction.Preconditions, Is.EqualTo(node.GpAction.Effects),
                    "a Persist action carries its literal unchanged from precondition to effect");
                Assert.That(node.InEdges, Has.Count.EqualTo(1),
                    "the Persist node must be wired to its literal node");
            }
        }

        [Test]
        public void ExpandLayer_CarriesActionEffectsIntoNextBeliefState() {
            var act = new GpAction("Act", new() { L("P(Obj)") }, new() { L("G(Obj)") });

            var layer = LayerWithBelief("P(Obj)");
            layer.ExpandActions(new List<GpAction> { act }, new Dictionary<ISentence, GpAction>());
            var next = layer.ExpandLayer();

            Assert.That(next.Level, Is.EqualTo(layer.Level + 1));
            var literals = next.BeliefState.GetLiteralNodes.Select(n => n.Literal.ToString()).ToList();
            Assert.That(literals, Does.Contain(L("G(Obj)").ToString()), "Act's effect must appear");
            Assert.That(literals, Does.Contain(L("P(Obj)").ToString()), "the persisted literal must appear");
        }
    }
}
