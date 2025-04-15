using System;

namespace HelloWorldApp
{
    class Program
    {
        static void Main(string[] args)
        {
            int x = 5;
            int[] arr =
            {
                1,
                2
            };
            int[, ] matrix =
            {
                {
                    1,
                    2
                },
                {
                    3,
                    4
                }
            };
            Console.WriteLine("Step 1");
            Console.WriteLine("Step 2");
        }
    }
}

public static class VariableTracker
{
    private static int _stepCounter = 0;
    public static void TrackVariables(params (string Name, object Value)[] variables)
    {
        var lines = new[]
        {
            string.Join("//", new object[] { System.Threading.Interlocked.Increment(ref _stepCounter), string.Join("//", variables.Select(v => $"{v.Name}//{(v.Value?.GetType().Name ?? "unknown")}//{(v.Value is Array ? ((Array)v.Value).Rank : 0)}//{(v.Value is Array ? string.Join(",", ((Array)v.Value).Cast<object>().Select(x => x?.ToString() ?? "null")) : (v.Value?.ToString() ?? "null"))}")) })
        };
        System.IO.File.AppendAllLines("/app/code_files/351201438values.txt", lines);
    }
}