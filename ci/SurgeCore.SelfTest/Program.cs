using System;
using System.Diagnostics;

namespace Surge.Ci
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            int matches = 2000;
            if (args.Length > 0 && int.TryParse(args[0], out int n) && n > 0)
                matches = n;

            var sw = Stopwatch.StartNew();
            try
            {
                string report = SurgeCore.SelfTest.Run(matches);
                sw.Stop();
                Console.WriteLine($"{report} ({sw.ElapsedMilliseconds} ms)");
                return 0;
            }
            catch (Exception e)
            {
                sw.Stop();
                Console.Error.WriteLine(
                    $"SelfTest FAILED after {sw.ElapsedMilliseconds} ms: {e.Message}");
                return 1;
            }
        }
    }
}
