using System;
using System.Linq;
using System.IO;
namespace HelloWorldApp
{
    class Program
    {
        static void Main(string[] args)
        {
            int[] arr = { 4, 3, 2, 1 };

            for (int i = 0; i < arr.Length; i++)
            {
                for (int j = arr.Length - 1; j > i; j--)
                {
                    if (arr[j] < arr[j - 1])
                    {
                        int sw = arr[j];
                        arr[j] = arr[j - 1];
                        arr[j - 1] = sw;
                    }
                }
            }
            VariableTracker.TrackVariables(1, ("i", i));
            VariableTracker.TrackVariables(2, ("i", i), ("j", j), ("arr", arr));
            VariableTracker.TrackVariables(3, ("arr", arr));
        }
    }
}

public static class VariableTracker
{
    private static int _stepCounter = 0;
    private static string _codeId = "25";

    public static void TrackVariables(int methodId, params (string Name, object Value)[] variables)
    {
        var lines = new System.Collections.Generic.List<string>();
        int step = System.Threading.Interlocked.Increment(ref _stepCounter);

        foreach (var v in variables)
        {
            if (v.Value == null)
            {
                lines.Add($"{step}//{methodId}//{v.Name}//unknown//0//null");
                continue;
            }

            string typeName = v.Value.GetType().Name;
            int rank = v.Value is Array ? ((Array)v.Value).Rank : 0;
            string valueString;

            if (rank == 0)
            {
                valueString = v.Value.ToString();
                lines.Add($"{step}//{methodId}//{v.Name}//{typeName}//0//{valueString}");
            }
            else if (rank == 1)
            {
                typeName = $"{v.Value.GetType().GetElementType().Name}[]";
                var array = (Array)v.Value;
                valueString = string.Join(",", array.Cast<object>().Select(x => x?.ToString() ?? "null"));
                lines.Add($"{step}//{methodId}//{v.Name}//{typeName}//1//{valueString}");
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
                    lines.Add($"{step}//{methodId}//{v.Name}//{typeName}//2//{valueString}");
                }
            }
        }

        System.IO.File.AppendAllLines(Path.Combine("/app/code_files/", $"{_codeId}values.txt"), lines);
    }
}