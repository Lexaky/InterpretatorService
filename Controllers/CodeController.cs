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

namespace InterpretatorService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CodeController : ControllerBase
    {
        private readonly IInterpreterService _interpreterService;
        private const string StorageDirectory = "/app/code_files";

        public CodeController(IInterpreterService interpreterService)
        {
            _interpreterService = interpreterService;

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
            var algorithmId = request.AlgorithmId;

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
                // Используем AlgorithmId как codeId
                int codeId = algorithmId;
                string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
                string metaFilePath = Path.Combine(StorageDirectory, $"{codeId}init.txt");
                string errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
                string warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
                string outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");

                // Проверяю, не существует ли уже файл
                if (System.IO.File.Exists(codeFilePath))
                {
                    return BadRequest($"Algorithm with ID {codeId} already exists.");
                }

                Console.WriteLine($"Saving code file to {codeFilePath}");
                // Сохраняю исходный код
                using (var stream = new System.IO.FileStream(codeFilePath, FileMode.Create))
                {
                    await codeFile.CopyToAsync(stream);
                }

                Console.WriteLine($"Saving meta file to {metaFilePath}");
                // Сохраняю метафайл
                using (var stream = new System.IO.FileStream(metaFilePath, FileMode.Create))
                {
                    await metaFile.CopyToAsync(stream);
                }

                // Читаю метаинформацию
                string[] metaLines;
                using (var stream = new StreamReader(metaFile.OpenReadStream()))
                {
                    metaLines = (await stream.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                }

                // Парсинг метафайла
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

                // Компиляция и выполнение исходного кода
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(codeFilePath);
                stopwatch.Stop();

                // Логирование результата выполнения
                Console.WriteLine($"ExecuteCodeAsync result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}, StandardOutput={codeModel.StandardOutput}");

                // Записываем результаты
                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                // Проверяю успешность
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

                // Устанавливаем делегат для TrackVariables
                VariableTracker.TrackDelegate = (filePath, variables) =>
                {
                    var tracker = new VariableTracker();
                    tracker.TrackVariables($"{StorageDirectory}/{filePath}values.txt", variables).GetAwaiter().GetResult();
                };

                return Ok(new { CodeId = codeId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UploadCodeFile exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, new CodeResponseDto
                {
                    Output = "",
                    Error = $"Server error: {ex.Message}",
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
                // Читаем исходный код
                string code = await System.IO.File.ReadAllTextAsync(codeFilePath);

                // Читаем метаинформацию
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

                // Модифицируем код
                string modifiedCode = ModifyCode(code, trackLines, codeId);
                await System.IO.File.WriteAllTextAsync(modifiedFilePath, modifiedCode);

                return Ok(new { CodeId = codeId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ModifyCode exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Server error: {ex.Message}");
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

                // Логируем результат
                Console.WriteLine($"ExecuteCode result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                // Обновляем файлы вывода
                var errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
                var warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
                var outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");

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