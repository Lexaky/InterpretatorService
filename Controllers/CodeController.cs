using InterpretatorService.DTOs;
using InterpretatorService.Interfaces;
using InterpretatorService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using InterpretatorService.Services;
using Microsoft.EntityFrameworkCore;
using InterpretatorService.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InterpretatorService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CodeController : ControllerBase
    {
        private readonly IInterpreterService _interpreterService;
        private readonly TestsDbContext _dbContext;
        private const string StorageDirectory = "/app/code_files";

        public CodeController(IInterpreterService interpreterService, TestsDbContext dbContext)
        {
            _interpreterService = interpreterService;
            _dbContext = dbContext;

            if (!Directory.Exists(StorageDirectory))
            {
                Console.WriteLine($"Creating directory: {StorageDirectory}");
                Directory.CreateDirectory(StorageDirectory);
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadCodeFile([FromForm] UploadCodeRequestDto request)
        {
            var codeFile = request.CodeFile;
            var metaFile = request.MetaFile;

            if (codeFile == null || codeFile.Length == 0 || Path.GetExtension(codeFile.FileName).ToLower() != ".cs")
            {
                return BadRequest("Please upload a valid .cs file.");
            }
            if (metaFile == null || metaFile.Length == 0 || Path.GetExtension(metaFile.FileName).ToLower() != ".txt")
            {
                return BadRequest("Please upload a valid .txt file.");
            }

            try
            {
                // Проверяем и создаём директорию StorageDirectory
                if (!Directory.Exists(StorageDirectory))
                {
                    Console.WriteLine($"Creating directory {StorageDirectory}...");
                    Directory.CreateDirectory(StorageDirectory);
                }

                Console.WriteLine("Attempting to connect to PostgreSQL...");
                int codeId;
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    Console.WriteLine("Creating new Algorithm entry...");
                    var newAlgorithm = new Algorithm { AlgoPath = "" };
                    _dbContext.Algorithms.Add(newAlgorithm);
                    Console.WriteLine("Saving changes to Algorithms...");
                    await _dbContext.SaveChangesAsync();
                    codeId = newAlgorithm.AlgoId;
                    Console.WriteLine($"Generated AlgoId: {codeId}");
                    await transaction.CommitAsync();
                }

                string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
                string metaFilePath = Path.Combine(StorageDirectory, $"{codeId}init.txt");

                if (System.IO.File.Exists(codeFilePath))
                {
                    return BadRequest($"Algorithm with ID {codeId} already exists.");
                }

                // Сохраняем файл .cs
                Console.WriteLine($"Saving code file to {codeFilePath}");
                using (var stream = new FileStream(codeFilePath, FileMode.Create))
                {
                    await codeFile.CopyToAsync(stream);
                    await stream.FlushAsync(); // Гарантируем запись на диск
                    Console.WriteLine($"Code file {codeFilePath} saved successfully.");
                }

                // Сохраняем метаданные
                Console.WriteLine($"Saving meta file to {metaFilePath}");
                using (var stream = new FileStream(metaFilePath, FileMode.Create))
                {
                    await metaFile.CopyToAsync(stream);
                    await stream.FlushAsync(); // Гарантируем запись на диск
                    Console.WriteLine($"Meta file {metaFilePath} saved successfully.");
                }

                // Обновляем AlgoPath
                Console.WriteLine($"Updating AlgoPath for AlgoId: {codeId}");
                var algorithm = await _dbContext.Algorithms.FindAsync(codeId);
                algorithm.AlgoPath = codeFilePath;
                await _dbContext.SaveChangesAsync();
                Console.WriteLine($"AlgoPath updated to {codeFilePath}");

                return Ok(new { CodeId = codeId });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    errorMessage += $"\nInner Exception: {ex.Message}";
                }
                Console.WriteLine($"UploadCodeFile exception: {errorMessage}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new CodeResponseDto
                {
                    Output = "",
                    Error = $"Server error: {errorMessage}",
                    ExecutionTime = 0,
                    IsSuccessful = false
                });
            }
        }

        [HttpPost("modify/{codeId}")]
        public async Task<IActionResult> ModifyCode(int codeId)
        {
            string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
            string metaFilePath = Path.Combine(StorageDirectory, $"{codeId}init.txt");
            string modifiedFilePath = Path.Combine(StorageDirectory, $"{codeId}modified.cs");
            string errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
            string warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
            string outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");
            string valuesFilePath = Path.Combine(StorageDirectory, $"{codeId}values.txt");
            string modifiedErrorsPath = Path.Combine(StorageDirectory, $"{codeId}modifiederrors.txt");
            string modifiedOutputPath = Path.Combine(StorageDirectory, $"{codeId}modifiedoutput.txt");
            string modifiedWarningsPath = Path.Combine(StorageDirectory, $"{codeId}modifiedwarnings.txt");

            if (!System.IO.File.Exists(codeFilePath))
            {
                return NotFound($"Code file with ID {codeId} not found.");
            }
            if (!System.IO.File.Exists(metaFilePath))
            {
                return NotFound($"Meta file with ID {codeId} not found.");
            }

            try
            {
                var filesToDelete = new[]
                {
                    modifiedFilePath, errorsPath, warningsPath, outputPath, valuesFilePath,
                    modifiedErrorsPath, modifiedOutputPath, modifiedWarningsPath
                };
                foreach (var file in filesToDelete)
                {
                    if (System.IO.File.Exists(file))
                    {
                        Console.WriteLine($"Deleting existing file: {file}");
                        System.IO.File.Delete(file);
                    }
                }

                Console.WriteLine($"Reading code file: {codeFilePath}");
                string code = await System.IO.File.ReadAllTextAsync(codeFilePath);

                Console.WriteLine($"Reading meta file: {metaFilePath}");
                var metaLines = await System.IO.File.ReadAllLinesAsync(metaFilePath);
                var trackLines = new List<(int LineNumber, string[] VariableNames)>();
                foreach (var line in metaLines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1 || !int.TryParse(parts[0], out int lineNumber))
                    {
                        Console.WriteLine($"Skipping invalid meta line: {line}");
                        continue;
                    }
                    var variableNames = parts.Skip(1).Select(v => v.Trim()).ToArray();
                    trackLines.Add((LineNumber: lineNumber, VariableNames: variableNames));
                }

                Console.WriteLine($"Modifying code for AlgoId: {codeId}");
                string modifiedCode = ModifyCode(code, trackLines, codeId);
                Console.WriteLine($"Writing modified code to: {modifiedFilePath}");
                await System.IO.File.WriteAllTextAsync(modifiedFilePath, modifiedCode);

                Console.WriteLine($"Removing existing AlgoSteps for AlgoId: {codeId}");
                var oldSteps = _dbContext.AlgoSteps.Where(s => s.AlgoId == codeId);
                _dbContext.AlgoSteps.RemoveRange(oldSteps);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine("Existing AlgoSteps removed successfully.");

                Console.WriteLine($"Executing modified code: {modifiedFilePath}");
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(modifiedFilePath);
                stopwatch.Stop();

                Console.WriteLine($"ExecuteCodeAsync result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                Console.WriteLine($"Writing errors to: {errorsPath}");
                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                Console.WriteLine($"Writing warnings to: {warningsPath}");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                Console.WriteLine($"Writing output to: {outputPath}");
                await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                if (!codeModel.IsSuccessful || !string.IsNullOrEmpty(codeModel.ErrorOutput))
                {
                    return StatusCode(500, new CodeResponseDto
                    {
                        Output = codeModel.StandardOutput ?? "",
                        Error = codeModel.ErrorOutput ?? "Unknown error occurred during code execution",
                        ExecutionTime = stopwatch.ElapsedMilliseconds,
                        IsSuccessful = false
                    });
                }

                if (System.IO.File.Exists(valuesFilePath))
                {
                    Console.WriteLine($"Reading values file: {valuesFilePath}");
                    var valueLines = await System.IO.File.ReadAllLinesAsync(valuesFilePath);

                    foreach (var line in valueLines)
                    {
                        Console.WriteLine($"Processing value line: {line}");
                        var parts = line.Split("//");
                        if (parts.Length != 5)
                        {
                            Console.WriteLine($"Skipping invalid value line: {line}");
                            continue;
                        }

                        if (!int.TryParse(parts[0], out int step))
                        {
                            Console.WriteLine($"Invalid step value in line: {line}");
                            continue;
                        }

                        var algoStep = new AlgoStep
                        {
                            AlgoId = codeId,
                            Step = step,
                            Type = parts[2],
                            VarName = parts[1],
                            Value = parts[4]
                        };

                        Console.WriteLine($"Adding AlgoStep: AlgoId={algoStep.AlgoId}, Step={algoStep.Step}, VarName={algoStep.VarName}, Value={algoStep.Value}");
                        _dbContext.AlgoSteps.Add(algoStep);
                    }

                    Console.WriteLine("Saving AlgoSteps to database...");
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine("AlgoSteps saved successfully.");
                }
                else
                {
                    Console.WriteLine($"Values file not found: {valuesFilePath}");
                }

                return Ok(new { CodeId = codeId });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    errorMessage += $"\nInner Exception: {ex.Message}";
                }
                Console.WriteLine($"ModifyCode exception: {errorMessage}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new CodeResponseDto
                {
                    Output = "",
                    Error = $"Server error: {errorMessage}",
                    ExecutionTime = 0,
                    IsSuccessful = false
                });
            }
        }

        [HttpPost("execute/{codeId}")]
        public async Task<IActionResult> ExecuteCode(int codeId)
        {
            string modifiedFilePath = Path.Combine(StorageDirectory, $"{codeId}modified.cs");
            if (!System.IO.File.Exists(modifiedFilePath))
            {
                return NotFound($"Modified code file with ID {codeId} not found.");
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(modifiedFilePath);
                stopwatch.Stop();

                Console.WriteLine($"ExecuteCode result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                var errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
                var warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
                string outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");

                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                var response = new CodeResponseDto
                {
                    Output = codeModel.StandardOutput ?? "",
                    Error = codeModel.ErrorOutput ?? "",
                    ExecutionTime = stopwatch.ElapsedMilliseconds,
                    IsSuccessful = codeModel.IsSuccessful
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ExecuteCode exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new CodeResponseDto
                {
                    Output = "",
                    Error = $"Server error: {ex.Message}",
                    ExecutionTime = 0,
                    IsSuccessful = false
                });
            }
        }
        [HttpGet("values/{codeId}")]
        public IActionResult GetValuesFile(int codeId)
        {
            string valuesFilePath = Path.Combine(StorageDirectory, $"{codeId}values.txt");
            if (!System.IO.File.Exists(valuesFilePath))
            {
                return NotFound("Values file not found.");
            }

            return PhysicalFile(valuesFilePath, "text/plain", Path.GetFileName(valuesFilePath));
        }

        [HttpGet("warnings/{codeId}")]
        public IActionResult GetWarningsFile(int codeId)
        {
            string warningFilePath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
            if (!System.IO.File.Exists(warningFilePath))
            {
                return NotFound("Warnings file not found.");
            }

            return PhysicalFile(warningFilePath, "text/plain", Path.GetFileName(warningFilePath));
        }

        [HttpGet("errors/{codeId}")]
        public IActionResult GetErrorsFile(int codeId)
        {
            string errorFilePath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
            if (!System.IO.File.Exists(errorFilePath))
            {
                return NotFound("Errors file not found.");
            }

            return PhysicalFile(errorFilePath, "text/plain", Path.GetFileName(errorFilePath));
        }

        [HttpGet("output/{codeId}")]
        public IActionResult GetOutputFile(int codeId)
        {
            string outputFilePath = Path.Combine(StorageDirectory, $"{codeId}output.txt");
            if (!System.IO.File.Exists(outputFilePath))
            {
                return NotFound("Output file not found.");
            }

            return PhysicalFile(outputFilePath, "text/plain", Path.GetFileName(outputFilePath));
        }

        [HttpPut("update/{codeId}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateCodeFile(int codeId, [FromForm] UpdateCodeRequestDto request)
        {
            if (request.CodeFile == null || request.CodeFile.Length == 0 || Path.GetExtension(request.CodeFile.FileName).ToLower() != ".cs")
            {
                return BadRequest("Please upload a valid .cs file.");
            }

            string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
            if (!System.IO.File.Exists(codeFilePath))
            {
                return NotFound($"Code file with ID {codeId} not found.");
            }

            try
            {
                // Обновляем исходный код
                using (var stream = new System.IO.FileStream(codeFilePath, FileMode.Create))
                {
                    await request.CodeFile.CopyToAsync(stream);
                }

                // Удаляем модифицированный код
                string modifiedFilePath = Path.Combine(StorageDirectory, $"{codeId}modified.cs");
                if (System.IO.File.Exists(modifiedFilePath))
                {
                    System.IO.File.Delete(modifiedFilePath);
                }

                // Компилируем и выполняем новый исходный код
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(codeFilePath);
                stopwatch.Stop();

                // Логируем результат
                Console.WriteLine($"UpdateCodeFile result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                // Обновляем файлы вывода
                var errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
                var warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
                var outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");

                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                if (!codeModel.IsSuccessful || !string.IsNullOrEmpty(codeModel.ErrorOutput))
                {
                    return StatusCode(500, new CodeResponseDto
                    {
                        Output = codeModel.StandardOutput ?? "",
                        Error = codeModel.ErrorOutput ?? "Unknown error occurred during code execution",
                        ExecutionTime = stopwatch.ElapsedMilliseconds,
                        IsSuccessful = false
                    });
                }

                return Ok(new { CodeId = codeId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateCodeFile exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        [HttpGet("variables/{codeId}")]
        public async Task<IActionResult> GetVariables(int codeId)
        {
            string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
            if (!System.IO.File.Exists(codeFilePath))
            {
                return NotFound($"Code file with ID {codeId} not found.");
            }

            try
            {
                string code = await System.IO.File.ReadAllTextAsync(codeFilePath);
                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var root = syntaxTree.GetRoot();

                // Собираем переменные и их типы
                var variables = new HashSet<string>();
                var declarations = root.DescendantNodes()
                    .OfType<VariableDeclaratorSyntax>()
                    .Select(v =>
                    {
                        var parent = v.Ancestors().OfType<VariableDeclarationSyntax>().FirstOrDefault();
                        string typeName = parent?.Type.ToString() ?? "unknown";
                        return $"{v.Identifier.Text} - {typeName}";
                    })
                    .Distinct();

                variables.UnionWith(declarations);

                // Добавляем параметры методов
                var parameters = root.DescendantNodes()
                    .OfType<ParameterSyntax>()
                    .Select(p => $"{p.Identifier.Text} - {p.Type?.ToString() ?? "unknown"}")
                    .Distinct();

                variables.UnionWith(parameters);

                // Создаем временный файл для результата
                string variablesFilePath = Path.Combine(StorageDirectory, $"{codeId}variables.txt");
                await System.IO.File.WriteAllLinesAsync(variablesFilePath, variables);

                return PhysicalFile(variablesFilePath, "text/plain", "variables.txt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetVariables exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        [HttpGet("list")]
        public IActionResult GetCodeFileIds()
        {
            try
            {
                var codeFiles = System.IO.Directory.GetFiles(StorageDirectory, "*.cs")
                    .Where(file => !file.EndsWith("modified.cs"))
                    .Select(file => int.Parse(Path.GetFileNameWithoutExtension(file)))
                    .OrderBy(id => id)
                    .ToList();

                return Ok(new { CodeIds = codeFiles });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetCodeFileIds exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        private string ModifyCode(string code, List<(int LineNumber, string[] VariableNames)> trackLines, int codeId)
        {
            // Парсим код в синтаксическое дерево
            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            var root = syntaxTree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
            {
                throw new InvalidOperationException("Failed to obtain CompilationUnitSyntax from the provided code.");
            }

            // Находим метод Main
            var mainMethod = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "Main");

            if (mainMethod == null)
            {
                throw new InvalidOperationException("Main method not found in the provided code.");
            }

            // Получаем тело метода Main
            var mainBody = mainMethod.Body;
            if (mainBody == null)
            {
                throw new InvalidOperationException("Main method body is empty.");
            }

            // Получаем все операторы с номерами строк
            var statementsWithLines = mainBody.Statements
                .Select((stmt, index) => (Statement: stmt, Line: stmt.GetLocation().GetLineSpan().StartLinePosition.Line))
                .ToList();

            var newStatements = mainBody.Statements.ToList();

            // Сортируем строки трекинга в обратном порядке для корректной вставки
            foreach (var (lineNumber, variableNames) in trackLines.OrderByDescending(t => t.LineNumber))
            {
                if (variableNames.Length == 0)
                    continue;

                // Формируем вызов TrackVariables
                string trackCall = $"VariableTracker.TrackVariables({string.Join(", ", variableNames.Select(name => $"(\"{name}\", {name})"))});";

                // Переводим 1-based строку файла в 0-based
                int targetLine = lineNumber - 1;

                // Находим ближайший оператор на этой или следующей строке
                int insertIndex = -1;
                for (int i = 0; i < statementsWithLines.Count; i++)
                {
                    if (statementsWithLines[i].Line >= targetLine)
                    {
                        insertIndex = i;
                        break;
                    }
                }

                // Если не нашли оператор на строке >= targetLine, вставляем в конец
                if (insertIndex == -1)
                {
                    insertIndex = newStatements.Count;
                }

                // Вставляем трекер
                var trackStatement = SyntaxFactory.ParseStatement(trackCall + "\n");
                newStatements.Insert(insertIndex, trackStatement);
            }

            // Обновляем тело метода Main
            var newMainBody = mainBody.WithStatements(SyntaxFactory.List(newStatements));
            var newMainMethod = mainMethod.WithBody(newMainBody);

            // Заменяем старый метод Main на новый
            var newRoot = root.ReplaceNode(mainMethod, newMainMethod) as CompilationUnitSyntax;

            if (newRoot == null)
            {
                throw new InvalidOperationException("Failed to obtain CompilationUnitSyntax after replacing Main method.");
            }

            // Парсим код трекера
            var trackerCodeText = new VariableTracker().GetTrackerMethodCode(codeId.ToString());
            var trackerSyntaxTree = CSharpSyntaxTree.ParseText(trackerCodeText);
            var trackerRoot = trackerSyntaxTree.GetRoot() as CompilationUnitSyntax;

            if (trackerRoot == null)
            {
                throw new InvalidOperationException("Failed to parse tracker code as CompilationUnitSyntax.");
            }

            // Извлекаем членов (класс VariableTracker)
            var trackerMembers = trackerRoot.Members;
            if (!trackerMembers.Any())
            {
                throw new InvalidOperationException("No members found in tracker code.");
            }

            // Извлекаем директивы using и добавляем только отсутствующие
            var existingUsings = newRoot.Usings.Select(u => u.Name.ToString()).ToHashSet();
            var trackerUsings = trackerRoot.Usings
                .Where(u => !existingUsings.Contains(u.Name.ToString()))
                .ToArray();

            // Добавляем директивы using и членов трекера
            newRoot = newRoot.AddUsings(trackerUsings).AddMembers(trackerMembers.ToArray());

            // Возвращаем код без переформатирования
            return newRoot.ToFullString();
        }
    }
}