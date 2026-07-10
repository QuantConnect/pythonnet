using System;
using System.Collections.Generic;
using NUnit.Framework;
using Python.Runtime;

namespace Python.EmbeddingTest
{
    /// <summary>
    /// The overload hint signatures must show the Python types a caller uses,
    /// following the conversions the runtime performs on arguments.
    /// </summary>
    public class TestMethodSignatureFormatter
    {
        private static string SignatureOf(string methodName, string displayName = null)
        {
            return MethodSignatureFormatter.FormatSignature(typeof(SampleTarget).GetMethod(methodName), displayName);
        }

        [Test]
        public void FormatsPrimitivesAsPythonTypes()
        {
            Assert.AreEqual(
                "primitives(count: int, price: float, ratio: float, scale: float, flag: bool, name: str, letter: str)",
                SignatureOf(nameof(SampleTarget.Primitives)));
        }

        [Test]
        public void FormatsTimeTypesAsDatetimeAndTimedelta()
        {
            Assert.AreEqual(
                "time_types(time: datetime, period: timedelta)",
                SignatureOf(nameof(SampleTarget.TimeTypes)));
        }

        [Test]
        public void FormatsNullablesAsOptional()
        {
            Assert.AreEqual(
                "nullables(start_time: Optional[timedelta] = None, max_count: Optional[int] = None)",
                SignatureOf(nameof(SampleTarget.Nullables)));
        }

        [Test]
        public void FormatsDelegatesAsCallable()
        {
            Assert.AreEqual(
                "delegates(selector: Callable[[datetime], int], handler: Callable[[str], None])",
                SignatureOf(nameof(SampleTarget.Delegates)));
        }

        [Test]
        public void FormatsCollectionsAsListAndDict()
        {
            Assert.AreEqual(
                "collections(names: List[str], values: List[int], prices: List[float], lookup: Dict[str, float])",
                SignatureOf(nameof(SampleTarget.Collections)));
        }

        [Test]
        public void FormatsObjectAndPyObjectAsAny()
        {
            Assert.AreEqual(
                "any_types(anything: Any, py_object: Any, py_list: List[Any], py_dict: Dict[Any, Any])",
                SignatureOf(nameof(SampleTarget.AnyTypes)));
        }

        [Test]
        public void KeepsClrOnlyTypeNames()
        {
            Assert.AreEqual(
                "clr_types(address: Uri, mode: StringComparison = StringComparison.ORDINAL)",
                SignatureOf(nameof(SampleTarget.ClrTypes)));
        }

        [Test]
        public void RendersConstructorsWithDisplayName()
        {
            var signature = MethodSignatureFormatter.FormatSignature(
                typeof(SampleTarget).GetConstructors()[0], nameof(SampleTarget));
            Assert.AreEqual("SampleTarget(period: timedelta, start_time: Optional[timedelta] = None)", signature);
        }

        [Test]
        public void SkipsPyObjectOverloadsFromHints()
        {
            var hint = MethodSignatureFormatter.FormatOverloads(typeof(MixedOverloadsTarget).GetConstructors(),
                displayName: nameof(MixedOverloadsTarget));

            StringAssert.Contains("The following overloads are available:", hint);
            StringAssert.Contains("MixedOverloadsTarget(period: timedelta)", hint);
            StringAssert.Contains("MixedOverloadsTarget(max_count: int)", hint);
            StringAssert.DoesNotContain("py_func", hint);
        }

        [Test]
        public void ShowsPyObjectOverloadsWhenThereIsNothingElseToHint()
        {
            var hint = MethodSignatureFormatter.FormatOverloads(typeof(PyObjectOnlyTarget).GetConstructors(),
                displayName: nameof(PyObjectOnlyTarget));

            StringAssert.Contains("The expected signature is:", hint);
            StringAssert.Contains("PyObjectOnlyTarget(py_func: Any)", hint);
        }

        private class MixedOverloadsTarget
        {
            public MixedOverloadsTarget(TimeSpan period)
            {
            }

            public MixedOverloadsTarget(int maxCount)
            {
            }

            public MixedOverloadsTarget(PyObject pyFunc)
            {
            }
        }

        private class PyObjectOnlyTarget
        {
            public PyObjectOnlyTarget(PyObject pyFunc)
            {
            }
        }

        private class SampleTarget
        {
            public SampleTarget(TimeSpan period, TimeSpan? startTime = null)
            {
            }

            public void Primitives(int count, double price, decimal ratio, float scale, bool flag, string name, char letter)
            {
            }

            public void TimeTypes(DateTime time, TimeSpan period)
            {
            }

            public void Nullables(TimeSpan? startTime = null, int? maxCount = null)
            {
            }

            public void Delegates(Func<DateTime, int> selector, Action<string> handler)
            {
            }

            public void Collections(List<string> names, IEnumerable<int> values, decimal[] prices, Dictionary<string, decimal> lookup)
            {
            }

            public void AnyTypes(object anything, PyObject pyObject, PyList pyList, PyDict pyDict)
            {
            }

            public void ClrTypes(Uri address, StringComparison mode = StringComparison.Ordinal)
            {
            }
        }
    }
}
