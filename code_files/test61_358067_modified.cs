using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;

class Program
{
    static void Main(string []args)
    await TestVariableTracker.Initialize("61_358067", "/app/code_files/61_358067_userdata.txt");
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
    private static readonly Dictionary<int, int> _trackerStepCounts = new Dictionary<int, int>();

    public static async Task Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        _mismatchKeys.Clear();
        _trackerStepCounts.Clear();

        await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
            $"[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n");
    }

    public class ValueData
    {
        public int StepNumber { get; set; }
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
        public string ActualValue { get; set; }
        public string ExpectedValue { get; set; }
    }

    public static async Task<(Dictionary<string, object> UpdatedValues, string Error)> TrackVariables(int methodId, int lineNumber, params (string Name, object Value)[] variables)
    {
        try
        {
            if (!_trackerStepCounts.ContainsKey(methodId))
                _trackerStepCounts[methodId] = 0;
            int stepNumber = ++_trackerStepCounts[methodId];

            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Started: stepNumber={stepNumber}, methodId={methodId}, lineNumber={lineNumber}, variables={string.Join(",", variables.Select(v => v.Name))}\n");

            var valuesLines = new List<string>();
            var mismatchesLines = new List<string>();
            var updatedValues = new Dictionary<string, object>();
            string error = null;

            var userData = await System.IO.File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 4 && int.TryParse(parts[0], out int step) && int.TryParse(parts[1], out int trackerId))
                .Select(parts => (StepNumber: int.Parse(parts[0]), TrackerHitId: int.Parse(parts[1]), VariableName: parts[2], Value: string.Join(" ", parts.Skip(3))))
                .ToList();

            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] User values: {string.Join(",", userValues.Select(kv => $"step={kv.StepNumber},tracker={kv.TrackerHitId},{kv.VariableName}={kv.Value}"))}\n");

            foreach (var (name, value) in variables)
            {
                if (value is Array arr && arr.Rank == 2)
                {
                    string typeName = $"{value.GetType().GetElementType().Name}[,]";
                    for (int i = 0; i < arr.GetLength(0); i++)
                    {
                        string rowValue = string.Join(",", Enumerable.Range(0, arr.GetLength(1))
                            .Select(j => arr.GetValue(i, j)?.ToString() ?? "null"));
                        valuesLines.Add($"{stepNumber}//{methodId}//{name}//{typeName}//2//{rowValue}");
                    }
                }
                else
                {
                    string stringValue = value switch
                    {
                        Array arrValue when arrValue.Rank == 1 => string.Join(",", arrValue.Cast<object>().Select(x => x?.ToString() ?? "null")),
                        _ => value?.ToString() ?? "null"
                    };

                    string typeName = value?.GetType().Name ?? "null";
                    int rank = value is Array arrayValue ? arrayValue.Rank : 0;
                    if (rank == 1)
                        typeName = $"{value.GetType().GetElementType().Name}[]";

                    valuesLines.Add($"{stepNumber}//{methodId}//{name}//{typeName}//{rank}//{stringValue}");
                }
            }

            var matchingUserValues = userValues.Where(uv => uv.StepNumber == stepNumber).ToList();
            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array arrValue when arrValue.Rank == 1 => string.Join(",", arrValue.Cast<object>().Select(x => x?.ToString() ?? "null")),
                    Array arrValue when arrValue.Rank == 2 => string.Join(";", Enumerable.Range(0, arrValue.GetLength(0))
                        .Select(i => string.Join(",", Enumerable.Range(0, arrValue.GetLength(1))
                            .Select(j => arrValue.GetValue(i, j)?.ToString() ?? "null")))),
                    _ => value?.ToString() ?? "null"
                };

                var userVars = matchingUserValues.Where(uv => uv.VariableName == name).ToList();
                foreach (var userValue in userVars)
                {
                    if (userValue.TrackerHitId != methodId)
                    {
                        string mismatchKey = $"{stepNumber}//{methodId}//{name}//{userValue.TrackerHitId}";
                        if (_mismatchKeys.Add(mismatchKey))
                        {
                            mismatchesLines.Add($"{methodId}//{lineNumber}//{name}//Пользователь на трекере {userValue.TrackerHitId}, текущий трекер {methodId}//{userValue.Value}");
                        }
                        error = $"Пользователь ушёл на другой трекер на шаге {stepNumber}: trackerHitId={userValue.TrackerHitId}, ожидался {methodId}";
                    }
                    else if (userValue.Value != stringValue)
                    {
                        string mismatchKey = $"{stepNumber}//{methodId}//{name}//{userValue.Value}";
                        if (_mismatchKeys.Add(mismatchKey))
                        {
                            mismatchesLines.Add($"{methodId}//{lineNumber}//{name}//{stringValue}//{userValue.Value}");
                        }
                        updatedValues[name] = UpdateVariable(value, userValue.Value, value?.GetType());
                    }
                }
            }

            var valuesPath = $"/app/code_files/{_codeId}values.txt";
            var mismatchesPath = $"/app/code_files/{_codeId}mismatches.txt";

            if (!System.IO.File.Exists(valuesPath))
            {
                await System.IO.File.WriteAllTextAsync(valuesPath, string.Empty);
                await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                    $"[{DateTime.Now}][TrackVariables] Created values file: {valuesPath}\n");
            }
            if (!System.IO.File.Exists(mismatchesPath))
            {
                await System.IO.File.WriteAllTextAsync(mismatchesPath, string.Empty);
                await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                    $"[{DateTime.Now}][TrackVariables] Created mismatches file: {mismatchesPath}\n");
            }

            await System.IO.File.AppendAllLinesAsync(valuesPath, valuesLines);
            await System.IO.File.AppendAllLinesAsync(mismatchesPath, mismatchesLines);

            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Completed: stepNumber={stepNumber}, methodId={methodId}, lineNumber={lineNumber}, valuesCount={valuesLines.Count}, mismatchesCount={mismatchesLines.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n");

            return (updatedValues, error);
        }
        catch (Exception ex)
        {
            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n");
            return (new Dictionary<string, object>(), $"Ошибка трекинга: {ex.Message}");
        }
    }

    public static async Task WriteRemainingUserValues(int methodId, int lineNumber)
    {
        try
        {
            var userData = await System.IO.File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 4 && int.TryParse(parts[0], out int step) && int.TryParse(parts[1], out int trackerId))
                .Select(parts => (StepNumber: int.Parse(parts[0]), TrackerHitId: int.Parse(parts[1]), VariableName: parts[2], Value: string.Join(" ", parts.Skip(3))))
                .ToList();

            var maxProgramStep = _trackerStepCounts.ContainsKey(methodId) ? _trackerStepCounts[methodId] : 0;
            var remainingUserValues = userValues.Where(uv => uv.TrackerHitId == methodId && uv.StepNumber > maxProgramStep).ToList();

            var mismatchesLines = new List<string>();
            foreach (var userValue in remainingUserValues)
            {
                string mismatchKey = $"{userValue.StepNumber}//{methodId}//{userValue.VariableName}//{userValue.Value}";
                if (_mismatchKeys.Add(mismatchKey))
                {
                    mismatchesLines.Add($"{methodId}//{lineNumber}//{userValue.VariableName}//Отсутствует//{userValue.Value}");
                }
            }

            if (mismatchesLines.Any())
            {
                var mismatchesPath = $"/app/code_files/{_codeId}mismatches.txt";
                if (!System.IO.File.Exists(mismatchesPath))
                {
                    await System.IO.File.WriteAllTextAsync(mismatchesPath, string.Empty);
                }
                await System.IO.File.AppendAllLinesAsync(mismatchesPath, mismatchesLines);

                await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                    $"[{DateTime.Now}][WriteRemainingUserValues] Added {mismatchesLines.Count} remaining user values to mismatches: methodId={methodId}, lineNumber={lineNumber}\n");
            }
        }
        catch (Exception ex)
        {
            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][WriteRemainingUserValues] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n");
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