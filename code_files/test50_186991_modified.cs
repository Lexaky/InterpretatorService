using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

class Program
{
    static void Main(string []args)
    {
        int[] numbers = { 5,10,15 };
        int sum = 0;

        foreach (int number in numbers)
        {
            sum += number;
            var updates_12_0 = await TestVariableTracker.TrackVariables(12, 12, ("sum", sum));
            if (updates_12_0.ContainsKey("sum")) sum = (int)updates_12_0["sum"];
        }

        Console.WriteLine($"Сумма: {sum}");
    }
}




public static class TestVariableTracker
{
    private static string _codeId;
    private static string _userDataPath;
    private static readonly HashSet<string> _mismatchKeys = new HashSet<string>();

    public static async Task Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        _mismatchKeys.Clear();

        await File.AppendAllTextAsync("/app/code_files/debug.txt",
            $"[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n");
    }

    public class ValueData
    {
        public int TrackerHitId { get; set; }
        public int LineNumber { get; set; }
        public string VariableName { get; set; }
        public string Type { get; set; }
        public int Rank { get; set; }
        public string Value { get; set; }
    }

    public class MismatchData
    {
        public int TrackerHitId { get; set; }
        public int LineNumber { get; set; }
        public string VariableName { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
    }

    public static async Task<Dictionary<string, object>> TrackVariables(int methodId, int lineNumber, params (string Name, object Value)[] variables)
    {
        try
        {
            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Started: methodId={methodId}, lineNumber={lineNumber}, variables={string.Join(",", variables.Select(v => v.Name))}\n");

            var valuesLines = new List<string>();
            var mismatchesLines = new List<string>();
            var userData = await File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out int id) && id == methodId)
                .ToDictionary(parts => parts[1], parts => parts[2]);

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] User values for methodId={methodId}: {string.Join(",", userValues.Select(kv => $"{kv.Key}={kv.Value}"))}\n");

            var updatedValues = new Dictionary<string, object>();

            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array array => string.Join(",", array.Cast<object>().Select(x => x?.ToString() ?? "null")),
                    _ => value?.ToString() ?? "null"
                };

                string typeName = value?.GetType().Name ?? "null";
                int rank = value is Array arr ? arr.Rank : 0;
                if (rank == 1)
                    typeName = $"{value.GetType().GetElementType().Name}[]"; 
                else if (rank == 2)
                    typeName = $"{value.GetType().GetElementType().Name}[,]";
                
                valuesLines.Add($"{lineNumber}//{methodId}//{name}//{typeName}//{rank}//{stringValue}");

                if (userValues.ContainsKey(name))
                {
                    if (userValues[name] != stringValue)
                    {
                        string mismatchKey = $"{methodId}//{lineNumber}//{name}";
                        if (_mismatchKeys.Add(mismatchKey))
                        {
                            mismatchesLines.Add($"{methodId}//{lineNumber}//{name}//{stringValue}//{userValues[name]}");
                        }
                        updatedValues[name] = UpdateVariable(value, userValues[name], value?.GetType());
                    }
                }
            }

            var valuesPath = $"{Directory.GetCurrentDirectory()}/app/code_files/{_codeId}values.txt";
            var mismatchesPath = $"{Directory.GetCurrentDirectory()}/app/code_files/{_codeId}mismatches.txt";

            if (!File.Exists(valuesPath))
            {
                await File.WriteAllTextAsync(valuesPath, string.Empty);
                await File.AppendAllTextAsync("/app/code_files/debug.txt",
                    $"[{DateTime.Now}][TrackVariables] Created values file: {valuesPath}\n");
            }
            if (!File.Exists(mismatchesPath))
            {
                await File.WriteAllTextAsync(mismatchesPath, string.Empty);
                await File.AppendAllTextAsync("/app/code_files/debug.txt",
                    $"[{DateTime.Now}][TrackVariables] Created mismatches file: {mismatchesPath}\n");
            }

            await File.AppendAllLinesAsync(valuesPath, valuesLines);
            await File.AppendAllLinesAsync(mismatchesPath, mismatchesLines);

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Completed: methodId={methodId}, lineNumber={lineNumber}, valuesCount={valuesLines.Count}, mismatchesCount={mismatchesLines.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n");

            return updatedValues;
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n");
            return new Dictionary<string, object>();
        }
    }

    private static object UpdateVariable(object variable, string userValue, Type variableType)
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