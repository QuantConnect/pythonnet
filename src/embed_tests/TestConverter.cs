using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using NUnit.Framework;

using Python.Runtime;

using PyRuntime = Python.Runtime.Runtime;

namespace Python.EmbeddingTest
{
    public class TestConverter
    {
        static readonly Type[] _numTypes = new Type[]
        {
                typeof(short),
                typeof(ushort),
                typeof(int),
                typeof(uint),
                typeof(long),
                typeof(ulong)
        };

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

        [Test]
        public void ConvertListRoundTrip()
        {
            var list = new List<Type> { typeof(decimal), typeof(int) };
            var py = list.ToPython();
            object result;
            var converted = Converter.ToManaged(py.Handle, typeof(List<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, list);
        }

        [Test]
        public void GenericList()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var converted = Converter.ToManaged(py.Handle, typeof(IList<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(typeof(List<Type>), result.GetType());
            Assert.AreEqual(2, ((IReadOnlyCollection<Type>) result).Count);
            Assert.AreEqual(typeof(decimal), ((IReadOnlyCollection<Type>) result).ToList()[0]);
            Assert.AreEqual(typeof(int), ((IReadOnlyCollection<Type>) result).ToList()[1]);
        }

        [Test]
        public void ReadOnlyCollection()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var converted = Converter.ToManaged(py.Handle, typeof(IReadOnlyCollection<Type>), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(typeof(List<Type>), result.GetType());
            Assert.AreEqual(2, ((IReadOnlyCollection<Type>) result).Count);
            Assert.AreEqual(typeof(decimal), ((IReadOnlyCollection<Type>) result).ToList()[0]);
            Assert.AreEqual(typeof(int), ((IReadOnlyCollection<Type>) result).ToList()[1]);
        }

        [Test]
        public void ConvertPyListToArray()
        {
            var array = new List<Type> { typeof(decimal), typeof(int) };
            var py = array.ToPython();
            object result;
            var outputType = typeof(Type[]);
            var converted = Converter.ToManaged(py.Handle, outputType, out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, array);
            Assert.AreEqual(outputType, result.GetType());
        }

        [Test]
        public void ConvertInvalidDateTime()
        {
            var number = 10;
            var pyNumber = number.ToPython();

            object result;
            var converted = Converter.ToManaged(pyNumber.Handle, typeof(DateTime), out result, false);

            Assert.IsFalse(converted);
        }

        [Test]
        public void ConvertTimeSpanRoundTrip()
        {
            var timespan = new TimeSpan(0, 1, 0, 0);
            var pyTimedelta = timespan.ToPython();

            object result;
            var converted = Converter.ToManaged(pyTimedelta.Handle, typeof(TimeSpan), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(result, timespan);
        }

        [Test]
        public void ConvertDecimalPerformance()
        {
            var value = 1111111111.0001m;

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 500000; i++)
            {
                var pyDecimal = value.ToPython();
                object result;
                var converted = Converter.ToManaged(pyDecimal.Handle, typeof(decimal), out result, false);
                if (!converted || result == null)
                {
                    throw new Exception("");
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Unspecified)]
        public void ConvertDateTimeRoundTripPerformance(DateTimeKind kind)
        {
            var datetime = new DateTime(2000, 1, 1, 2, 3, 4, 5, kind);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var i = 0; i < 500000; i++)
            {
                var pyDatetime = datetime.ToPython();
                object result;
                var converted = Converter.ToManaged(pyDatetime.Handle, typeof(DateTime), out result, false);
                if (!converted || result == null)
                {
                    throw new Exception("");
                }
            }
            stopwatch.Stop();
            Console.WriteLine($"Took: {stopwatch.ElapsedMilliseconds}");
        }

        [Test]
        public void ConvertDateTimeRoundTripNoTime()
        {
            var datetime = new DateTime(2000, 1, 1);
            var pyDatetime = datetime.ToPython();

            object result;
            var converted = Converter.ToManaged(pyDatetime.Handle, typeof(DateTime), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(datetime, result);
        }

        [TestCase(DateTimeKind.Utc)]
        [TestCase(DateTimeKind.Unspecified)]
        public void ConvertDateTimeRoundTrip(DateTimeKind kind)
        {
            var datetime = new DateTime(2000, 1, 1, 2, 3, 4, 5, kind);
            var pyDatetime = datetime.ToPython();

            object result;
            var converted = Converter.ToManaged(pyDatetime.Handle, typeof(DateTime), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(datetime, result);
        }

        [Test]
        public void ConvertTimestampRoundTrip()
        {
            var timeSpan = new TimeSpan(1, 2, 3, 4, 5);
            var pyTimeSpan = timeSpan.ToPython();

            object result;
            var converted = Converter.ToManaged(pyTimeSpan.Handle, typeof(TimeSpan), out result, false);

            Assert.IsTrue(converted);
            Assert.AreEqual(timeSpan, result);
        }

        [Test]
        public void TestConvertSingleToManaged(
            [Values(float.PositiveInfinity, float.NegativeInfinity, float.MinValue, float.MaxValue, float.NaN,
                float.Epsilon)] float testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat.Handle, typeof(float), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.IsTrue(((float) convertedValue).Equals(testValue));
        }

        [Test]
        public void TestConvertDoubleToManaged(
            [Values(double.PositiveInfinity, double.NegativeInfinity, double.MinValue, double.MaxValue, double.NaN,
                double.Epsilon)] double testValue)
        {
            var pyFloat = new PyFloat(testValue);

            object convertedValue;
            var converted = Converter.ToManaged(pyFloat.Handle, typeof(double), out convertedValue, false);

            Assert.IsTrue(converted);
            Assert.IsTrue(((double) convertedValue).Equals(testValue));
        }

        [Test]
        public void CovertTypeError()
        {
            Type[] floatTypes = new Type[]
            {
                typeof(float),
                typeof(double)
            };
            using (var s = new PyString("abc"))
            {
                foreach (var type in _numTypes.Union(floatTypes))
                {
                    object value;
                    try
                    {
                        bool res = Converter.ToManaged(s.Handle, type, out value, true);
                        Assert.IsFalse(res);
                        var bo = Exceptions.ExceptionMatches(Exceptions.TypeError);
                        Assert.IsTrue(Exceptions.ExceptionMatches(Exceptions.TypeError)
                            || Exceptions.ExceptionMatches(Exceptions.ValueError));
                    }
                    finally
                    {
                        Exceptions.Clear();
                    }
                }
            }
        }

        [Test]
        public void ConvertOverflow()
        {
            using (var num = new PyLong(ulong.MaxValue))
            {
                IntPtr largeNum = PyRuntime.PyNumber_Add(num.Handle, num.Handle);
                try
                {
                    object value;
                    foreach (var type in _numTypes)
                    {
                        bool res = Converter.ToManaged(largeNum, type, out value, true);
                        Assert.IsFalse(res);
                        Assert.IsTrue(Exceptions.ExceptionMatches(Exceptions.OverflowError));
                        Exceptions.Clear();
                    }
                }
                finally
                {
                    Exceptions.Clear();
                    PyRuntime.XDecref(largeNum);
                }
            }
        }

        [Test]
        public void RawListProxy()
        {
            var list = new List<string> {"hello", "world"};
            var listProxy = PyObject.FromManagedObject(list);
            var clrObject = (CLRObject)ManagedType.GetManagedObject(listProxy.Handle);
            Assert.AreSame(list, clrObject.inst);
        }

        [Test]
        public void RawPyObjectProxy()
        {
            var pyObject = "hello world!".ToPython();
            var pyObjectProxy = PyObject.FromManagedObject(pyObject);
            var clrObject = (CLRObject)ManagedType.GetManagedObject(pyObjectProxy.Handle);
            Assert.AreSame(pyObject, clrObject.inst);

            var proxiedHandle = pyObjectProxy.GetAttr("Handle").As<IntPtr>();
            Assert.AreEqual(pyObject.Handle, proxiedHandle);
        }

        [Test]
        public void PrimitiveIntConversion()
        {
            decimal value = 10;
            var pyValue = value.ToPython();

            // Try to convert python value to int
            var testInt = pyValue.As<int>();
            Assert.AreEqual(testInt , 10);
        }

    }
}
