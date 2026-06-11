using System.Collections.Generic;
using System.Linq;
using FirstOrderLogic;
using NUnit.Framework;

namespace FolTests {
    // Shared fixture: a fresh parser per test plus small parsing helpers.
    public abstract class TestBase {
        protected FirstOrderLogic.FirstOrderLogic Logic = null!;

        [SetUp]
        public void BaseSetup() => Logic = new FirstOrderLogic.FirstOrderLogic();

        protected ISentence S(string s) => (ISentence)Logic.TryParse(s);
        protected List<ISentence> Set(params string[] sentences) => sentences.Select(S).ToList();
    }
}
