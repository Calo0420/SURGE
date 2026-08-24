using System;
using System.Diagnostics;
using Surge.Timing;

namespace Surge.Ci
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            int cases = 5000;
            if (args.Length > 0 && int.TryParse(args[0], out int n) && n > 0) cases = n;

            var sw = Stopwatch.StartNew();
            try
            {
                string report = MatchClockSelfTest.Run(cases);
                sw.Stop();
                Console.WriteLine($"{report} ({sw.ElapsedMilliseconds} ms)");
                return 0;
            }
            catch (Exception e)
            {
                sw.Stop();
                Console.Error.WriteLine(
                    $"MatchClockSelfTest FAILED after {sw.ElapsedMilliseconds} ms: {e.Message}");
                return 1;
            }
        }
    }
}
