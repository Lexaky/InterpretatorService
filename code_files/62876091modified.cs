using System;
using System.Linq;
namespace HelloWorldApp
{
    class Program
    {
        static void Main(string[] args)
        {
            int x = 5;
VariableTracker.TrackVariables(("x", x), ("arr", arr), ("matrix", matrix));
            int[] arr = { 1, 2 };
VariableTracker.TrackVariables(("x", x), ("arr", arr), ("matrix", matrix));
            int[,] matrix = { { 1, 2 }, { 3, 4 } };
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
        var lines = new System.Collections.Generic.List<string>();
        int step = System.Threading.Interlocked.Increment(ref _stepCounter);

        foreach (var v in variables)
        {
            if (v.Value == null)
            {
                lines.Add($"{step}//{v.Name}//unknown//0//null");
                continue;
            }

            string typeName = v.Value.GetType().Name;
            int rank = v.Value is Array ? ((Array)v.Value).Rank : 0;
            string valueString;

            if (rank == 0)
            {
                valueString = v.Value.ToString();
                lines.Add($"{step}//{v.Name}//{typeName}//0//{valueString}");
            }
            else if (rank == 1)
            {
                typeName = $"{v.Value.GetType().GetElementType().Name}[]";
                var array = (Array)v.Value;
                valueString = string.Join(",", array.Cast<object>().Select(x => x?.ToString() ?? "null"));
                lines.Add($"{step}//{v.Name}//{typeName}//1//{valueString}");
            }
            else if (rank == 2)
            {
                typeName = $"{v.Value.GetType().GetElementType().Name}[,]";
                var array = (Array)v.Value;
                for (int i = 0; i < array.GetLength(0); i++)
                {
                    var rowValues = new System.Collections.Generic.List<object>();
                    for (int j = 0; j < array.GetLength(1); j++)
                    {
                        rowValues.Add(array.GetValue(i, j));
                    }
                    valueString = string.Join(",", rowValues.Select(x => x?.ToString() ?? "null"));
                    lines.Add($"{step}//{v.Name}//{typeName}//2//{valueString}");
                }
            }
        }

        System.IO.File.AppendAllLines("/app/code_files/62876091values.txt", lines);
    }
}