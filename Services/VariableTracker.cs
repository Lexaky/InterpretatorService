using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InterpretatorService.Services
{
    public class VariableTracker
    {
        private readonly List<string> _valuesOutput = new List<string>();
        private static int _stepCounter;

        public static Action<string, (string Name, object Value)[]> TrackDelegate { get; set; }

        public string GetTrackerMethodCode(string codeId)
        {
            return $@"
using System;
using System.Linq;

public static class VariableTracker
{{
    private static int _stepCounter = 0;

    public static void TrackVariables(params (string Name, object Value)[] variables)
    {{
        var lines = new System.Collections.Generic.List<string>();
        int step = System.Threading.Interlocked.Increment(ref _stepCounter);

        foreach (var v in variables)
        {{
            if (v.Value == null)
            {{
                lines.Add($""{{step}}//{{v.Name}}//unknown//0//null"");
                continue;
            }}

            string typeName = v.Value.GetType().Name;
            int rank = v.Value is Array ? ((Array)v.Value).Rank : 0;
            string valueString;

            if (rank == 0)
            {{
                valueString = v.Value.ToString();
                lines.Add($""{{step}}//{{v.Name}}//{{typeName}}//0//{{valueString}}"");
            }}
            else if (rank == 1)
            {{
                typeName = $""{{v.Value.GetType().GetElementType().Name}}[]"";
                var array = (Array)v.Value;
                valueString = string.Join("","", array.Cast<object>().Select(x => x?.ToString() ?? ""null""));
                lines.Add($""{{step}}//{{v.Name}}//{{typeName}}//1//{{valueString}}"");
            }}
            else if (rank == 2)
            {{
                typeName = $""{{v.Value.GetType().GetElementType().Name}}[,]"";
                var array = (Array)v.Value;
                for (int i = 0; i < array.GetLength(0); i++)
                {{
                    var rowValues = new System.Collections.Generic.List<object>();
                    for (int j = 0; j < array.GetLength(1); j++)
                    {{
                        rowValues.Add(array.GetValue(i, j));
                    }}
                    valueString = string.Join("","", rowValues.Select(x => x?.ToString() ?? ""null""));
                    lines.Add($""{{step}}//{{v.Name}}//{{typeName}}//2//{{valueString}}"");
                }}
            }}
        }}

        System.IO.File.AppendAllLines(""/app/code_files/{codeId}values.txt"", lines);
    }}
}}";
        }

        public async Task TrackVariables(string outputFilePath, params (string Name, object Value)[] variables)
        {
            _stepCounter++;
            var stepLines = new List<string>();

            foreach (var (name, value) in variables)
            {
                if (value == null)
                {
                    stepLines.Add($"{_stepCounter}//{name}//unknown//0//null");
                    continue;
                }

                string typeName = value.GetType().Name;
                int dimensionality = 0;
                string[] valuesStrings;

                if (value.GetType().IsArray)
                {
                    if (value is Array array)
                    {
                        dimensionality = array.Rank;
                        if (dimensionality == 1)
                        {
                            typeName = $"{array.GetType().GetElementType().Name}[]";
                            valuesStrings = new[] { string.Join(",", array.Cast<object>().Select(v => v?.ToString() ?? "null")) };
                        }
                        else if (dimensionality == 2)
                        {
                            typeName = $"{array.GetType().GetElementType().Name}[,]";
                            valuesStrings = new string[array.GetLength(0)];
                            for (int i = 0; i < array.GetLength(0); i++)
                            {
                                var rowValues = new List<object>();
                                for (int j = 0; j < array.GetLength(1); j++)
                                {
                                    rowValues.Add(array.GetValue(i, j));
                                }
                                valuesStrings[i] = string.Join(",", rowValues.Select(v => v?.ToString() ?? "null"));
                            }
                        }
                        else
                        {
                            valuesStrings = new[] { "unsupported" };
                        }
                    }
                    else
                    {
                        valuesStrings = new[] { "unknown" };
                    }
                }
                else
                {
                    valuesStrings = new[] { value.ToString() };
                }

                foreach (var valuesString in valuesStrings)
                {
                    stepLines.Add($"{_stepCounter}//{name}//{typeName}//{dimensionality}//{valuesString}");
                }
            }

            _valuesOutput.AddRange(stepLines);
            await File.WriteAllLinesAsync(outputFilePath, _valuesOutput);
        }

        public static bool IsVariableInitialized(string code, string variableName, int initLine)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot();
            var variableDeclarations = root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .Where(v => v.Identifier.Text == variableName);

            foreach (var decl in variableDeclarations)
            {
                var lineSpan = decl.GetLocation().GetLineSpan();
                if (lineSpan.StartLinePosition.Line + 1 == initLine && decl.Initializer != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}