using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;

class Program
{
    static void Main(string []args)
    {
        int[] arr = { 5,4,3,2,1 };


        for (int i = 0; i < arr.Length - 1; i++)
        {
            for (int j = 0; j < arr.Length - i - 1; j++)
            {
                if (arr[j] > arr[j + 1])
                {
                    int temp = arr[j];
                    arr[j] = arr[j + 1];
                    arr[j + 1] = temp;
                }
                var updates_19_2 = TestVariableTracker.TrackVariables(19, ("arr", arr), ("i", i), ("j", j));
                if (updates_19_2.ContainsKey("arr")) arr = (int[])updates_19_2["arr"];
                if (updates_19_2.ContainsKey("i")) i = (int)updates_19_2["i"];
                if (updates_19_2.ContainsKey("j")) j = (int)updates_19_2["j"];
            }
            var updates_20_1 = TestVariableTracker.TrackVariables(20, ("arr", arr), ("i", i));
            if (updates_20_1.ContainsKey("arr")) arr = (int[])updates_20_1["arr"];
            if (updates_20_1.ContainsKey("i")) i = (int)updates_20_1["i"];
        }
        var updates_21_0 = TestVariableTracker.TrackVariables(21, ("arr", arr));
        if (updates_21_0.ContainsKey("arr")) arr = (int[])updates_21_0["arr"];

        Console.WriteLine("Отсортированный массив: " + string.Join(", ", arr));
    }
}



public static class TestVariableTracker
{
    private static int _stepCounter = 0;
    private static string _codeId = "40_588679";
    private static Dictionary<int, List<(string Name, string Value)>> _userValues;
    private static List<string> _mismatches = new List<string>();

    public static void Initialize(string codeId, string userDataFile)
    {
        _codeId = codeId;
        _stepCounter = 0;
        _mismatches.Clear();
        _userValues = new Dictionary<int, List<(string Name, string Value)>>();

        if (System.IO.File.Exists(userDataFile))
        {
            var lines = System.IO.File.ReadAllLines(userDataFile).Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[0], out int step))
                {
                    var value = string.Join(" ", parts.Skip(2));
                    if (!_userValues.ContainsKey(step))
                        _userValues[step] = new List<(string Name, string Value)>();
                    _userValues[step].Add((parts[1], value));
                }
            }
        }
    }

    public static Dictionary<string, object> TrackVariables(int methodId, params (string Name, object Value)[] variables)
    {
        var lines = new List<string>();
        int step = Interlocked.Increment(ref _stepCounter);
        var updatedValues = new Dictionary<string, object>();

        if (_userValues.ContainsKey(step))
        {
            var userVars = _userValues[step];
            foreach (var v in variables)
            {
                var userVar = userVars.FirstOrDefault(uv => uv.Name == v.Name);
                if (userVar != default)
                {
                    string progValue = FormatValue(v.Value);
                    if (progValue != userVar.Value)
                    {
                        _mismatches.Add($"{step}//{v.Name}//{progValue}//{userVar.Value}");
                        updatedValues[v.Name] = UpdateVariable(v.Value, userVar.Value, v.Value?.GetType(), v.Name);
                    }
                }
            }
        }

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
                    var rowValues = new List<object>();
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
        if (_mismatches.Any())
            System.IO.File.AppendAllLines(Path.Combine("/app/code_files/", $"{_codeId}mismatches.txt"), _mismatches);

        return updatedValues;
    }

    private static string FormatValue(object value)
    {
        if (value == null)
            return "null";
        if (value is Array array)
        {
            if (array.Rank == 1)
                return string.Join(",", array.Cast<object>().Select(x => x?.ToString() ?? "null"));
            if (array.Rank == 2)
            {
                var rows = new List<string>();
                for (int i = 0; i < array.GetLength(0); i++)
                {
                    var rowValues = new List<object>();
                    for (int j = 0; j < array.GetLength(1); j++)
                        rowValues.Add(array.GetValue(i, j));
                    rows.Add(string.Join(",", rowValues.Select(x => x?.ToString() ?? "null")));
                }
                return string.Join(";", rows);
            }
        }
        return value.ToString();
    }

    private static object UpdateVariable(object variable, string userValue, Type variableType, string variableName)
    {
        if (variableType == null || userValue == null)
            return variable;

        if (variableType == typeof(int))
        {
            if (int.TryParse(userValue, out int value))
                return value;
        }
        else if (variableType == typeof(float))
        {
            if (float.TryParse(userValue.Replace(".", ","), out float value))
                return value;
        }
        else if (variableType == typeof(double))
        {
            if (double.TryParse(userValue.Replace(".", ","), out double value))
                return value;
        }
        else if (variableType == typeof(char))
        {
            if (userValue.StartsWith("'") && userValue.EndsWith("'") && userValue.Length == 3)
                return userValue[1];
        }
        else if (variableType == typeof(string))
        {
            if (userValue.StartsWith("\"") && userValue.EndsWith("\""))
                return userValue.Substring(1, userValue.Length - 2);
        }
        else if (variableType == typeof(int[]))
        {
            var values = userValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => int.Parse(v.Trim()))
                .ToArray();
            return values;
        }
        else if (variableType == typeof(float[]))
        {
            var values = userValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => float.Parse(v.Trim().Replace(".", ",")))
                .ToArray();
            return values;
        }
        else if (variableType == typeof(double[]))
        {
            var values = userValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v.Trim().Replace(".", ",")))
                .ToArray();
            return values;
        }
        else if (variableType == typeof(int[,]))
        {
            var userVars = _userValues[_stepCounter].Where(uv => uv.Name == variableName).ToList();
            var rows = userVars.Select(uv => uv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => int.Parse(v.Trim()))
                .ToArray()).ToArray();
            if (rows.Length > 0 && rows[0].Length > 0)
            {
                var newArray = new int[rows.Length, rows[0].Length];
                for (int i = 0; i < rows.Length; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        newArray[i, j] = rows[i][j];
                return newArray;
            }
        }
        else if (variableType == typeof(float[,]))
        {
            var userVars = _userValues[_stepCounter].Where(uv => uv.Name == variableName).ToList();
            var rows = userVars.Select(uv => uv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => float.Parse(v.Trim().Replace(".", ",")))
                .ToArray()).ToArray();
            if (rows.Length > 0 && rows[0].Length > 0)
            {
                var newArray = new float[rows.Length, rows[0].Length];
                for (int i = 0; i < rows.Length; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        newArray[i, j] = rows[i][j];
                return newArray;
            }
        }
        else if (variableType == typeof(double[,]))
        {
            var userVars = _userValues[_stepCounter].Where(uv => uv.Name == variableName).ToList();
            var rows = userVars.Select(uv => uv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v.Trim().Replace(".", ",")))
                .ToArray()).ToArray();
            if (rows.Length > 0 && rows[0].Length > 0)
            {
                var newArray = new double[rows.Length, rows[0].Length];
                for (int i = 0; i < rows.Length; i++)
                    for (int j = 0; j < rows[i].Length; j++)
                        newArray[i, j] = rows[i][j];
                return newArray;
            }
        }

        return variable;
    }
}