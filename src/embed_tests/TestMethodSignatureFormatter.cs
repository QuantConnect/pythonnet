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
                "primitives(int count, float price, float ratio, float scale, bool flag, str name, str letter)",
                SignatureOf(nameof(SampleTarget.Primitives)));
        }

        [Test]
        public void FormatsTimeTypesAsDatetimeAndTimedelta()
        {
            Assert.AreEqual(
                "time_types(datetime time, timedelta period)",
                SignatureOf(nameof(SampleTarget.TimeTypes)));
        }

        [Test]
        public void FormatsNullablesAsOptional()
        {
            Assert.AreEqual(
                "nullables(Optional[timedelta] start_time = None, Optional[int] max_count = None)",
                SignatureOf(nameof(SampleTarget.Nullables)));
        }

        [Test]
        public void FormatsDelegatesAsCallable()
        {
            Assert.AreEqual(
                "delegates(Callable[[datetime], int] selector, Callable[[str], None] handler)",
                SignatureOf(nameof(SampleTarget.Delegates)));
        }

        [Test]
        public void FormatsCollectionsAsListAndDict()
        {
            Assert.AreEqual(
                "collections(List[str] names, List[int] values, List[float] prices, Dict[str, float] lookup)",
                SignatureOf(nameof(SampleTarget.Collections)));
        }

        [Test]
        public void FormatsObjectAndPyObjectAsAny()
        {
            Assert.AreEqual(
                "any_types(Any anything, Any py_object, List[Any] py_list, Dict[Any, Any] py_dict)",
                SignatureOf(nameof(SampleTarget.AnyTypes)));
        }

        [Test]
        public void KeepsClrOnlyTypeNames()
        {
            Assert.AreEqual(
                "clr_types(Uri address, StringComparison mode = StringComparison.ORDINAL)",
                SignatureOf(nameof(SampleTarget.ClrTypes)));
        }

        [Test]
        public void RendersConstructorsWithDisplayName()
        {
            var signature = MethodSignatureFormatter.FormatSignature(
                typeof(SampleTarget).GetConstructors()[0], nameof(SampleTarget));
            Assert.AreEqual("SampleTarget(timedelta period, Optional[timedelta] start_time = None)", signature);
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
