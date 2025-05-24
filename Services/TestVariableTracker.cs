using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class TestVariableTracker
{
    private static string _codeId;
    private static string _userDataPath;

    public static void Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        File.AppendAllTextAsync("/app/code_files/debug.txt",
            $"[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n").GetAwaiter().GetResult();
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

            var values = new List<ValueData>();
            var mismatches = new List<MismatchData>();
            var userData = await File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out int id) && id == methodId)
                .ToDictionary(parts => parts[1], parts => parts[2]);

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] User values for methodId={methodId}: {string.Join(",", userValues.Select(kv => $"{kv.Key}={kv.Value}"))}\n");

            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array array => string.Join(",", array.Cast<object>()),
                    _ => value?.ToString() ?? "null"
                };

                values.Add(new ValueData
                {
                    TrackerHitId = methodId,
                    LineNumber = lineNumber,
                    VariableName = name,
                    Type = value?.GetType().Name ?? "null",
                    Rank = value is Array arr ? arr.Rank : 0,
                    Value = stringValue
                });

                if (userValues.ContainsKey(name))
                {
                    if (userValues[name] != stringValue)
                    {
                        mismatches.Add(new MismatchData
                        {
                            LineNumber = lineNumber,
                            VariableName = name,
                            ExpectedValue = userValues[name],
                            ActualValue = stringValue
                        });
                    }
                }
            }

            var valuesPath = $"/app/code_files/{_codeId}_temp_values.json";
            var mismatchesPath = $"/app/code_files/{_codeId}_temp_mismatches.json";

            await File.WriteAllTextAsync(valuesPath, JsonSerializer.Serialize(values));
            await File.WriteAllTextAsync(mismatchesPath, JsonSerializer.Serialize(mismatches));

            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Completed: methodId={methodId}, lineNumber={lineNumber}, valuesCount={values.Count}, mismatchesCount={mismatches.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n");

            return variables.ToDictionary(v => v.Name, v => v.Value);
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync("/app/code_files/debug.txt",
                $"[{DateTime.Now}][TrackVariables] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n");
            return new Dictionary<string, object>();
        }
    }

    public static string GetTrackerMethodCode(string codeId)
    {
        return @"
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public static class TestVariableTracker
{
    private static string _codeId;
    private static string _userDataPath;

    public static void Initialize(string codeId, string userDataPath)
    {
        _codeId = codeId;
        _userDataPath = userDataPath;
        File.AppendAllTextAsync(""/app/code_files/debug.txt"",
            $""[{DateTime.Now}][TestVariableTracker] Initialized: codeId={codeId}, userDataPath={userDataPath}\n"").GetAwaiter().GetResult();
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
        public int LineNumber { get; set; }
        public string VariableName { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
    }

    public static async Task<Dictionary<string, object>> TrackVariables(int methodId, int lineNumber, params (string Name, object Value)[] variables)
    {
        try
        {
            await File.AppendAllTextAsync(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Started: methodId={methodId}, lineNumber={lineNumber}, variables={string.Join("","", variables.Select(v => v.Name))}\n"");

            var values = new List<ValueData>();
            var mismatches = new List<MismatchData>();
            var userData = await File.ReadAllLinesAsync(_userDataPath);
            var userValues = userData.Skip(1)
                .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length >= 3 && int.TryParse(parts[0], out int id) && id == methodId)
                .ToDictionary(parts => parts[1], parts => parts[2]);

            await File.AppendAllTextAsync(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] User values for methodId={methodId}: {string.Join("","", userValues.Select(kv => $""{kv.Key}={kv.Value}""))}\n"");

            foreach (var (name, value) in variables)
            {
                string stringValue = value switch
                {
                    Array array => string.Join("","", array.Cast<object>()),
                    _ => value?.ToString() ?? ""null""
                };

                values.Add(new ValueData
                {
                    TrackerHitId = methodId,
                    LineNumber = lineNumber,
                    VariableName = name,
                    Type = value?.GetType().Name ?? ""null"",
                    Rank = value is Array arr ? arr.Rank : 0,
                    Value = stringValue
                });

                if (userValues.ContainsKey(name))
                {
                    if (userValues[name] != stringValue)
                    {
                        mismatches.Add(new MismatchData
                        {
                            LineNumber = lineNumber,
                            VariableName = name,
                            ExpectedValue = userValues[name],
                            ActualValue = stringValue
                        });
                    }
                }
            }

            var valuesPath = $""{Directory.GetCurrentDirectory()}/app/code_files/{_codeId}_temp_values.json"";
            var mismatchesPath = $""{Directory.GetCurrentDirectory()}/app/code_files/{_codeId}_temp_mismatches.json"";

            await File.WriteAllTextAsync(valuesPath, JsonSerializer.Serialize(values));
            await File.WriteAllTextAsync(mismatchesPath, JsonSerializer.Serialize(mismatches));

            await File.AppendAllTextAsync(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Completed: methodId={methodId}, lineNumber={lineNumber}, valuesCount={values.Count}, mismatchesCount={mismatches.Count}, valuesPath={valuesPath}, mismatchesPath={mismatchesPath}\n"");

            return variables.ToDictionary(v => v.Name, v => v.Value);
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(""/app/code_files/debug.txt"",
                $""[{DateTime.Now}][TrackVariables] Error: methodId={methodId}, lineNumber={lineNumber}, message={ex.Message}, stackTrace={ex.StackTrace}\n"");
            return new Dictionary<string, object>();
        }
    }
}";
    }
}