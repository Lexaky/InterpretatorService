using InterpretatorService.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using InterpretatorService.Interfaces;

namespace InterpretatorService.Services
{
    public class InterpreterService : IInterpreterService
    {
        public async Task<CodeModel> ExecuteCodeAsync(string codeFilePath)
        {
            if (!System.IO.File.Exists(codeFilePath))
            {
                Console.WriteLine($"Code file not found: {codeFilePath}");
                return new CodeModel(0, codeFilePath)
                {
                    ErrorOutput = $"Code file not found: {codeFilePath}",
                    IsSuccessful = false
                };
            }

            // Извлекаю codeId из имени файла
            string fileName = Path.GetFileNameWithoutExtension(codeFilePath);
            if (!int.TryParse(fileName, out int codeId))
            {
                Console.WriteLine($"Invalid codeId from file name: {fileName}");
                codeId = 0;
            }

            var codeModel = new CodeModel(codeId, codeFilePath);

            try
            {
                // Читаю код из файла
                string code = await System.IO.File.ReadAllTextAsync(codeFilePath);
                Console.WriteLine($"Read code from {codeFilePath}, length: {code.Length} chars");

                // Создаю синтаксическое дерево
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create(
                    $"TempAssembly_{codeId}",
                    syntaxTrees: new[] { syntaxTree },
                    references: GetMetadataReferences(),
                    options: new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                        .WithPlatform(Platform.AnyCpu)
                        .WithUsings("System") // Добавляю using System по умолчанию
                );

                // Компилирую код в память
                using var ms = new MemoryStream();
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var errors = string.Join("\n", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.GetMessage()));
                    var warnings = string.Join("\n", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => d.GetMessage()));

                    Console.WriteLine($"Compilation failed: {errors}");
                    codeModel.ErrorOutput = errors;
                    codeModel.WarningOutput = warnings;
                    codeModel.IsSuccessful = false;

                    await System.IO.File.WriteAllTextAsync(codeModel.ErrorFilePath, codeModel.ErrorOutput);
                    await System.IO.File.WriteAllTextAsync(codeModel.WarningFilePath, codeModel.WarningOutput);
                    await System.IO.File.WriteAllTextAsync(codeModel.OutputFilePath, "");

                    return codeModel;
                }

                Console.WriteLine("Compilation successful");

                // Выполняю скомпилированный код
                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());
                var entryPoint = assembly.EntryPoint;

                if (entryPoint == null)
                {
                    string errorMsg = "No entry point found in the compiled assembly.";
                    Console.WriteLine(errorMsg);
                    codeModel.ErrorOutput = errorMsg;
                    codeModel.IsSuccessful = false;

                    await System.IO.File.WriteAllTextAsync(codeModel.ErrorFilePath, codeModel.ErrorOutput);
                    await System.IO.File.WriteAllTextAsync(codeModel.OutputFilePath, "");
                    return codeModel;
                }

                // Перенаправление вывода на консоль
                using var outputWriter = new StringWriter();
                Console.SetOut(outputWriter);

                try
                {
                    // Выполнение Main
                    entryPoint.Invoke(null, new object[] { new string[0] });
                    codeModel.StandardOutput = outputWriter.ToString();
                    codeModel.IsSuccessful = true;

                    Console.WriteLine($"Execution successful, output: {codeModel.StandardOutput}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Execution failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
                    codeModel.ErrorOutput = $"Execution failed: {ex.Message}";
                    codeModel.IsSuccessful = false;
                }
                finally
                {
                    // Восстановление стандартного вывода
                    var standardOutput = new StreamWriter(Console.OpenStandardOutput());
                    standardOutput.AutoFlush = true;
                    Console.SetOut(standardOutput);
                }

                // Запись результатов
                await System.IO.File.WriteAllTextAsync(codeModel.OutputFilePath, codeModel.StandardOutput ?? "");
                await System.IO.File.WriteAllTextAsync(codeModel.ErrorFilePath, codeModel.ErrorOutput ?? "");
                await System.IO.File.WriteAllTextAsync(codeModel.WarningFilePath, codeModel.WarningOutput ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecuteCodeAsync exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                codeModel.ErrorOutput = $"Unexpected error: {ex.Message}";
                codeModel.IsSuccessful = false;

                await System.IO.File.WriteAllTextAsync(codeModel.ErrorFilePath, codeModel.ErrorOutput);
                await System.IO.File.WriteAllTextAsync(codeModel.OutputFilePath, "");
                await System.IO.File.WriteAllTextAsync(codeModel.WarningFilePath, "");
            }

            return codeModel;
        }

        private IEnumerable<MetadataReference> GetMetadataReferences()
        {
            var references = new List<MetadataReference>();

            // Добавляем ключевые сборки
            var assemblyNames = new[]
            {
                typeof(object).Assembly.Location, // mscorlib или System.Runtime
                typeof(Console).Assembly.Location, // System.Console
                typeof(Enumerable).Assembly.Location, // System.Linq
                typeof(List<>).Assembly.Location, // System.Collections
                typeof(Task).Assembly.Location, // System.Threading.Tasks
                typeof(StringWriter).Assembly.Location, // System.IO
                typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location, // System.ComponentModel.Annotations
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Core.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "netstandard.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Collections.dll"),
                Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.IO.dll")
            };

            foreach (var assemblyPath in assemblyNames.Distinct())
            {
                if (System.IO.File.Exists(assemblyPath))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(assemblyPath));
                        Console.WriteLine($"Added reference: {Path.GetFileName(assemblyPath)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to add reference {Path.GetFileName(assemblyPath)}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Assembly not found: {assemblyPath}");
                }
            }

            // Проверяем наличие критических сборок
            if (!references.Any(r => r.Display.Contains("System.Runtime")))
            {
                Console.WriteLine("Warning: System.Runtime.dll not included in references.");
            }
            if (!references.Any(r => r.Display.Contains("mscorlib")))
            {
                Console.WriteLine("Warning: mscorlib.dll not included in references.");
            }

            return references;
        }
    }
}