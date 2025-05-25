
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class TestVariableTracker
{
    private static string _codeId;
    private static string _userDataPath;
    private static int _stepCounter = 0;
    private static readonly HashSet<string> _mismatches = new HashSet<string>();

    public static async Task Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        _stepCounter = 0;
        _mismatches.Clear();

        await File.AppendAllTextAsync("/app/code_files/debug.txt",
            $"[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n");
    }

    public static async Task<Dictionary<string, object>> TrackVariables(int methodId, int lineNumber, params (string Name, object Value)[] variables)
    {
        try
        {
            int step = Interlocked.Increment(ref _stepCounter);
            var valuesLines = new List<string>();
            var updatedValues = new Dictionary<string, object>();

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Started: methodId={methodId}, step={step}, lineNumber={lineNumber}, variables={string.Join(",", variables.Select(v => v.Name))}\n");

            var userData = await File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out int id) && id == methodId)
                .ToDictionary(parts => parts[1], parts => string.Join(" ", parts.Skip(2)));

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] User values for methodId={methodId}: {string.Join(",", userValues.Select(kv => $"{kv.Key}={kv.Value}"))}\n");

            foreach (var (name, value) in variables)
            {
                string stringValue = FormatValue(value);
                string typeName = value?.GetType().Name ?? "unknown";
                int rank = value is Array arr ? arr.Rank : 0;

                if (rank == 1)
                    typeName = $"{value.GetType().GetElementType().Name}[]";
                else if (rank == 2)
                    typeName = $"{value.GetType().GetElementType().Name}[,]";

                valuesLines.Add($"{step}//{methodId}//{lineNumber}//{name}//{typeName}//{rank}//{stringValue}");

                if (userValues.ContainsKey(name))
                {
                    if (userValues[name] != stringValue)
                    {
                        string mismatchKey = $"{methodId}//{lineNumber}//{name}";
                        if (_mismatches.Add(mismatchKey))
                        {
                            await File.AppendAllTextAsync($"/app/code_files/{_codeId}mismatches.txt",
                                $"{step}//{methodId}//{lineNumber}//{name}//{stringValue}//{userValues[name]}\n");
                        }
                        updatedValues[name] = UpdateVariable(value, userValues[name], value?.GetType(), name);
                    }
                }
            }

            await File.AppendAllLinesAsync($"/app/code_files/{_codeId}values.txt", valuesLines);

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Completed: methodId={methodId}, step={step}, lineNumber={lineNumber}, valuesCount={valuesLines.Count}, mismatchesCount={_mismatches.Count}\n");

            return updatedValues;
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n");
            return new Dictionary<string, object>();
        }
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
            var rows = userValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => int.Parse(v.Trim()))
                    .ToArray())
                .ToArray();
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
            var rows = userValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => float.Parse(v.Trim().Replace(".", ",")))
                    .ToArray())
                .ToArray();
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
            var rows = userValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => double.Parse(v.Trim().Replace(".", ",")))
                    .ToArray())
                .ToArray();
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
using System;

class Program
{
    static async Task Main(string []args)
    {
    await TestVariableTracker.Initialize("48_365464", "/app/code_files/48_365464_userdata.txt");
        int[] numbers = { 4,3,2,10 };
        int sum = 0;

        foreach (int number in numbers)
        var updates_12_0 = await TestVariableTracker.TrackVariables(12, 12, ("sum", sum));
        if (updates_12_0.ContainsKey("sum")) sum = (int)updatesVar["sum"];
        {
            sum += number;
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}