using System.Linq;
using System.IO;
using System.Collections.Generic;
using System;

class Program
{
    static void Main(string []args)
    {
        int[] arr = { 5, 3, 8,
         1, 4 };

        for (int i = 0; i < arr.Length - 1; i++)
        {
            for (int j = 0; j < arr.Length - i - 1; j++)
            {
                if (arr[j] > arr[j + 1])
                {
                    int temp = arr[j];
                    arr[j] = arr[j + 1];
                    arr[j + 1] = temp;
                    var updates = VariableTracker.TrackVariables(3, ("arr", arr), ("j", j), ("i", i));
                    if (updates.ContainsKey("arr")) arr = updates["arr"];
                    if (updates.ContainsKey("j")) j = updates["j"];
                    if (updates.ContainsKey("i")) i = updates["i"];
                }
                var updates = VariableTracker.TrackVariables(2, ("arr", arr), ("i", i));
                if (updates.ContainsKey("arr")) arr = updates["arr"];
                if (updates.ContainsKey("i")) i = updates["i"];
            }
            var updates = VariableTracker.TrackVariables(1, ("arr", arr));
            if (updates.ContainsKey("arr")) arr = updates["arr"];
        }

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }
}



public static class VariableTracker
{
    private static int _stepCounter = 0;
    private static string _codeId = "43";

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