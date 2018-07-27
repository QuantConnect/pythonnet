using System;
using Python.Runtime;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace mixedmode
{
    class Program
    {
        static void Main(string[] args)
        {
            var algorithm = new AlgorithmWrapper();

            Task.Factory.StartNew(() =>
            {
                algorithm.Run();

            }).ContinueWith((ant) =>
            {
                algorithm.Initialize();

            }).ContinueWith((ant) =>
            {
                algorithm.OnData(DateTime.UtcNow.ToString());
                algorithm.OnData(DateTime.UtcNow.ToString());
                algorithm.OnData(DateTime.UtcNow.ToString());

            }).Wait();

            Console.Write($"{algorithm}. Press any key to exit.");
            Console.Read();
        }
    }

    public class AlgorithmWrapper : Algorithm.Algorithm
    {
        private dynamic _algorithm = null;

        public void Run()
        {
            using (Py.GIL())
            {
                Console.WriteLine($"Embedded Python {PythonEngine.Version}");
                var module = Py.Import("algorithm");

                _algorithm = module.GetAttr("MyAlgorithm").Invoke();

                if ((int)(module as dynamic).DEBUG == 1)
                {
                    Console.WriteLine("waiting for .NET debugger to attach");
                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(100);
                    }
                    Console.WriteLine(".NET debugger is attached");
                }
            }
            PythonEngine.BeginAllowThreads();
        }

        public override void Initialize()
        {
            using (Py.GIL())
            {
                _algorithm.Initialize();
            }
        }

        public override void OnData(string data)
        {
            using (Py.GIL())
            {
                _algorithm.OnData(data);
            }
        }

        public override string ToString()
        {
            using (Py.GIL())
            {
                return $"{_algorithm}";
            }
        }
    }
}
