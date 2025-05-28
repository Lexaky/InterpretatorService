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
    private static readonly HashSet<string> _mismatchKeys = new HashSet<string>();

    public static async Task Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        _mismatchKeys.Clear();

        await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
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
            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Started: methodId={methodId}, lineNumber={lineNumber}, variables={string.Join(",", variables.Select(v => v.Name))}\n");

            var valuesLines = new List<string>();
            var mismatchesLines = new List<string>();
            var userData = await System.IO.File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out int id) && id == methodId)
                .Select(parts => (VariableName: parts[1], Value: parts[2]))
                .ToList();

            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] User values for methodId={methodId}: {string.Join(",", userValues.Select(kv => $"{kv.VariableName}={kv.Value}"))}\n");

            var updatedValues = new Dictionary<string, object>();

            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array array when array.Rank == 1 => string.Join(",", array.Cast<object>().Select(x => x?.ToString() ?? "null")),
                    Array array when array.Rank == 2 => string.Join(";", Enumerable.Range(0, array.GetLength(0))
                        .Select(i => string.Join(",", Enumerable.Range(0, array.GetLength(1))
                            .Select(j => array.GetValue(i, j)?.ToString() ?? "null")))),
                    _ => value?.ToString() ?? "null"
                };

                string typeName = value?.GetType().Name ?? "null";
                int rank = value is Array arr ? arr.Rank : 0;
                if (rank == 1)
                    typeName = $"{value.GetType().GetElementType().Name}[]";
                else if (rank == 2)
                    typeName = $"{value.GetType().GetElementType().Name}[,]";

                valuesLines.Add($"{lineNumber}//{methodId}//{name}//{typeName}//{rank}//{stringValue}");

                var matchingUserValues = userValues.Where(uv => uv.VariableName == name).ToList();
                foreach (var userValue in matchingUserValues)
                {
                    if (userValue.Value != stringValue)
                    {
                        string mismatchKey = $"{methodId}//{lineNumber}//{name}//{userValue.Value}";
                        if (_mismatchKeys.Add(mismatchKey))
                        {
                            mismatchesLines.Add($"{methodId}//{lineNumber}//{name}//{stringValue}//{userValue.Value}");
                        }
                    }
                    updatedValues[name] = UpdateVariable(value, userValue.Value, value?.GetType());
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
                $"[{DateTime.Now}][TrackVariables] Completed: methodId={methodId}, lineNumber={lineNumber}, valuesCount={valuesLines.Count}, mismatchesCount={mismatchesLines.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n");

            return updatedValues;
        }
        catch (Exception ex)
        {
            await System.IO.File.AppendAllTextAsync("/app/code_files/debug.txt",
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

    public static string GetTrackerMethodCode(string codeId)
    {
        return @"
public static class TestVariableTracker
{
    private static string _codeId;
    private static string _userDataPath;
    private static readonly HashSet<string> _mismatchKeys = new HashSet<string>();
    private static int _currentStep = 0; // Текущий шаг выполнения

    public static void Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        _mismatchKeys.Clear();
        _currentStep = 0; // Сбрасываем шаг при инициализации

        File.AppendAllText(""/app/code_files/debug.txt"",
            $""[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n"");
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
        public string ExpectedValue { get; set; } // Значение программы
        public string ActualValue { get; set; }   // Значение пользователя
    }

    public static Dictionary<string, object> TrackVariables(int methodId, int lineNumber, params (string Name, object Value)[] variables)
    {
        try
        {
            _currentStep++; // Увеличиваем шаг при каждом вызове

            File.AppendAllText(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Started: step={_currentStep}, methodId={methodId}, lineNumber={lineNumber}, variables={string.Join("","", variables.Select(v => v.Name))}\n"");

            var valuesLines = new List<string>();
            var mismatchesLines = new List<string>();
            var userData = File.ReadAllLines(_userDataPath);
            var userValues = userData
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 4 && int.TryParse(parts[0], out int step) && step == _currentStep && int.TryParse(parts[1], out int trackerId) && trackerId == methodId)
                .Select(parts => (VariableName: parts[2], Value: parts[3]))
                .ToList();

            // Проверяем, есть ли данные для текущего шага с неверным трекером
            var wrongTrackerData = userData
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 4 && int.TryParse(parts[0], out int step) && step == _currentStep && int.TryParse(parts[1], out int trackerId) && trackerId != methodId)
                .ToList();

            File.AppendAllText(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] User values for step={_currentStep}, methodId={methodId}: {string.Join("","", userValues.Select(kv => $""{kv.VariableName}={kv.Value}""))}\n"");

            var updatedValues = new Dictionary<string, object>();

            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array array when array.Rank == 1 => string.Join("","", array.Cast<object>().Select(x => x?.ToString() ?? ""null"")),
                    Array array when array.Rank == 2 => string.Join("";"", Enumerable.Range(0, array.GetLength(0))
                        .Select(i => string.Join("","", Enumerable.Range(0, array.GetLength(1))
                            .Select(j => array.GetValue(i, j)?.ToString() ?? ""null"")))),
                    _ => value?.ToString() ?? ""null""
                };

                string typeName = value?.GetType().Name ?? ""null"";
                int rank = value is Array arr ? arr.Rank : 0;
                if (rank == 1)
                    typeName = $""{value.GetType().GetElementType().Name}[]"";
                else if (rank == 2)
                    typeName = $""{value.GetType().GetElementType().Name}[,]"";

                valuesLines.Add($""{_currentStep}//{methodId}//{name}//{typeName}//{rank}//{stringValue}"");

                // Проверяем, есть ли данные с неверным трекером для текущего шага
                if (wrongTrackerData.Any())
                {
                    string mismatchKey = $""{_currentStep}//{methodId}//{lineNumber}//{name}//wrong_tracker"";
                    if (_mismatchKeys.Add(mismatchKey))
                    {
                        mismatchesLines.Add($""{_currentStep}//{methodId}//{lineNumber}//{name}//пользователь ушёл в другой шаг"");
                    }
                    updatedValues[name] = value; // Не подменяем значение
                    continue; // Пропускаем дальнейшую обработку этой переменной
                }

                var matchingUserValues = userValues.Where(uv => uv.VariableName == name).ToList();
                foreach (var userValue in matchingUserValues)
                {
                    if (userValue.Value != stringValue)
                    {
                        string mismatchKey = $""{_currentStep}//{methodId}//{lineNumber}//{name}//{userValue.Value}"";
                        if (_mismatchKeys.Add(mismatchKey))
                        {
                            mismatchesLines.Add($""{_currentStep}//{methodId}//{lineNumber}//{name}//{stringValue}//{userValue.Value}"");
                        }
                    }
                    updatedValues[name] = UpdateVariable(value, userValue.Value, value?.GetType());
                }

                // Если пользовательских данных для переменной нет, сохраняем исходное значение
                if (!matchingUserValues.Any())
                {
                    updatedValues[name] = value;
                }
            }

            var valuesPath = $""/app/code_files/{_codeId}values.txt"";
            var mismatchesPath = $""/app/code_files/{_codeId}mismatches.txt"";

            if (!File.Exists(valuesPath))
            {
                File.WriteAllText(valuesPath, string.Empty);
                File.AppendAllText(""/app/code_files/debug.txt"",
                    $""[{DateTime.Now}][TrackVariables] Created values file: {valuesPath}\n"");
            }
            if (!File.Exists(mismatchesPath))
            {
                File.WriteAllText(mismatchesPath, string.Empty);
                File.AppendAllText(""/app/code_files/debug.txt"",
                    $""[{DateTime.Now}][TrackVariables] Created mismatches file: {mismatchesPath}\n"");
            }

            File.AppendAllLines(valuesPath, valuesLines);
            File.AppendAllLines(mismatchesPath, mismatchesLines);

            File.AppendAllText(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Completed: step={_currentStep}, methodId={methodId}, lineNumber={lineNumber}, valuesCount={valuesLines.Count}, mismatchesCount={mismatchesLines.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n"");

            return updatedValues;
        }
        catch (Exception ex)
        {
            File.AppendAllText(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Error: step={_currentStep}, methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n"");
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
            if (float.TryParse(userValue.Replace(""."", "",""), out float value))
                return value;
        }
        else if (variableType == typeof(double))
        {
            if (double.TryParse(userValue.Replace(""."", "",""), out double value))
                return value;
        }
        else if (variableType == typeof(char))
        {
            if (userValue.StartsWith(""'"") && userValue.EndsWith(""'"") && userValue.Length == 3)
                return userValue[1];
        }
        else if (variableType == typeof(string))
        {
            if (userValue.StartsWith(""\"""") && userValue.EndsWith(""\""""))
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
                .Select(v => float.Parse(v.Trim().Replace(""."", "","")))
                .ToArray();
            return values;
        }
        else if (variableType == typeof(double[]))
        {
            var values = userValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => double.Parse(v.Trim().Replace(""."", "","")))
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
                    .Select(v => float.Parse(v.Trim().Replace(""."", "","")))
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
                    .Select(v => double.Parse(v.Trim().Replace(""."", "","")))
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
}";
    }
}