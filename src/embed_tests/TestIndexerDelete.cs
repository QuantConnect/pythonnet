using System.Collections.Concurrent;
using System.Collections.Generic;

using NUnit.Framework;

using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// `del ob[key]` reaches mp_ass_subscript with a null value. It must raise a catchable Python
    /// exception (or delete, for IDictionary/IList types) instead of aborting the process.
    /// </summary>
    [TestFixture]
    public class TestIndexerDelete
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            PythonEngine.Initialize();
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            PythonEngine.Shutdown();
        }

        public class SettableIndexer
        {
            private readonly Dictionary<int, string> _items = new();

            public string this[int key]
            {
                get => _items[key];
                set => _items[key] = value;
            }

            public string Marker => "alive";
        }

        [Test]
        public void DelOnSettableIndexerRaisesTypeError()
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                scope.Set("ob", new SettableIndexer().ToPython());
                scope.Exec(@"
ob[1] = 'one'
raised = None
try:
    del ob[1]
except TypeError as e:
    raised = e
");
                using var raised = scope.Get("raised");
                Assert.IsFalse(raised.IsNone(), "del must raise TypeError");
                Assert.AreEqual("alive", scope.Eval("ob.Marker").As<string>());
                Assert.AreEqual("one", scope.Eval("ob[1]").As<string>());
            }
        }

        [Test]
        public void DelOnConcurrentDictionaryRemovesKey()
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                var dict = new ConcurrentDictionary<string, string>();
                dict["MyKey"] = "MyValue";
                scope.Set("d", dict.ToPython());

                scope.Exec("del d['MyKey']");

                Assert.IsFalse(dict.ContainsKey("MyKey"));
                Assert.AreEqual(0, scope.Eval("d.Count").As<int>());
            }
        }

        [Test]
        public void DelOnDictionaryMissingKeyRaisesKeyError()
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                scope.Set("d", new Dictionary<string, int> { ["a"] = 1 }.ToPython());
                scope.Exec(@"
raised = None
try:
    del d['missing']
except KeyError as e:
    raised = e
");
                using var raised = scope.Get("raised");
                Assert.IsFalse(raised.IsNone(), "del of a missing key must raise KeyError");
                Assert.AreEqual(1, scope.Eval("d.Count").As<int>());
            }
        }
    }
}
