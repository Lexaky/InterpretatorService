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
using Npgsql.Internal;
using System.Text;

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

            if (codeFile == null || codeFile.Length == 0 || Path.GetExtension(codeFile.FileName).ToLower() != ".cs")
            {
                using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                    await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] wrong cs file uploaded...");
                return BadRequest("Please upload a valid .cs file.");
            }

            try
            {
                // Проверяем и создаём директорию StorageDirectory
                if (!Directory.Exists(StorageDirectory))
                {
                    Console.WriteLine($"Creating directory {StorageDirectory}...");
                    using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                        await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Creating directory /app/code_files...");
                    Directory.CreateDirectory(StorageDirectory);
                }

                Console.WriteLine("Attempting to connect to PostgreSQL...");
                using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                    await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Attempting to connect to PostgreSQL...");
                int codeId;
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    Console.WriteLine("Creating new Algorithm entry...");
                    using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                        await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Creating new Algorithm entry...");
                    var newAlgorithm = new Algorithm { AlgoPath = "" };
                    _dbContext.Algorithms.Add(newAlgorithm);
                    Console.WriteLine("Saving changes to 'Algorithms' table...");
                    using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                        await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Saving changes to 'Algorithms' table...");
                    await _dbContext.SaveChangesAsync();
                    codeId = newAlgorithm.AlgoId;
                    Console.WriteLine($"Generated AlgoId: {codeId}");
                    using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                        await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] {codeId}; Generated AlgoId: {codeId}");
                    await transaction.CommitAsync();
                }

                string codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");

                if (System.IO.File.Exists(codeFilePath))
                {
                    return BadRequest($"Algorithm with ID {codeId} already exists.");
                }

                // Сохраняем файл .cs
                Console.WriteLine($"Saving code file to {codeFilePath}");
                using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                    await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Attempt to save code file to {codeFilePath}");
                using (var stream = new FileStream(codeFilePath, FileMode.Create))
                {
                    await codeFile.CopyToAsync(stream);
                    await stream.FlushAsync(); // Гарантируем запись на диск
                    Console.WriteLine($"Code file {codeFilePath} saved successfully.");
                    using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                        await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Successfully saved code file to {codeFilePath}");
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
                using (StreamWriter writer = new StreamWriter(Path.Combine(StorageDirectory, "logs.txt"), true))
                    await writer.WriteLineAsync($"[{DateTime.Now}][Launch Subsystem] Undefined Error next trace:\nUploadCodeFile exception: {errorMessage}\nStackTrace: {ex.StackTrace}");
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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] code file with ID " + codeId.ToString() + " was not found");
                return NotFound($"Code file with ID {codeId} not found.");
            }
            if (!System.IO.File.Exists(metaFilePath))
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] metafile with ID " + codeId.ToString() + " was not found");
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
                        using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                            writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] rewriting txt files with ID " + codeId.ToString());
                        Console.WriteLine($"Deleting existing file: {file}");
                        System.IO.File.Delete(file);
                    }
                }
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] reading code file " + codeFilePath.ToString());
                Console.WriteLine($"Reading code file: {codeFilePath}");
                string code = await System.IO.File.ReadAllTextAsync(codeFilePath);

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] reading metafile " + metaFilePath.ToString());
                Console.WriteLine($"Reading meta file: {metaFilePath}");
                var metaLines = await System.IO.File.ReadAllLinesAsync(metaFilePath);
                var trackLines = new List<(int LineNumber, string[] VariableNames)>();
                foreach (var line in metaLines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 1 || !int.TryParse(parts[0], out int lineNumber))
                    {
                        using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                            writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] skip metafile line: " + line.ToString());
                        Console.WriteLine($"Skipping invalid meta line: {line}");
                        continue;
                    }
                    var variableNames = parts.Skip(1).Select(v => v.Trim()).ToArray();
                    trackLines.Add((LineNumber: lineNumber, VariableNames: variableNames));
                }
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Modifying code for AlgoId: " + codeId.ToString());
                Console.WriteLine($"Modifying code for AlgoId: {codeId}");
                string modifiedCode = ModifyCode(code, trackLines, codeId);

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Writing modified code to: " + modifiedFilePath.ToString());
                Console.WriteLine($"Writing modified code to: {modifiedFilePath}");
                await System.IO.File.WriteAllTextAsync(modifiedFilePath, modifiedCode);

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Removing existing AlgoSteps for AlgoId: " + codeId.ToString());
                Console.WriteLine($"Removing existing AlgoSteps for AlgoId: {codeId}");
                var oldSteps = _dbContext.AlgoSteps.Where(s => s.AlgoId == codeId);
                _dbContext.AlgoSteps.RemoveRange(oldSteps);
                await _dbContext.SaveChangesAsync();
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Existing AlgoSteps removed successfully");
                Console.WriteLine("Existing AlgoSteps removed successfully.");

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Executing modified code: " + modifiedFilePath.ToString());
                Console.WriteLine($"Executing modified code: {modifiedFilePath}");
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(modifiedFilePath);
                stopwatch.Stop();

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem]" + $" ExecuteCodeAsync result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");
                Console.WriteLine($"ExecuteCodeAsync result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Writing errors to: " + errorsPath.ToString());
                Console.WriteLine($"Writing errors to: {errorsPath}");
                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Writing warnings to: " + warningsPath.ToString());
                Console.WriteLine($"Writing warnings to: {warningsPath}");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Writing output to: " + outputPath.ToString());
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
                    using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                        writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Reading values file: " + valuesFilePath.ToString());
                    Console.WriteLine($"Reading values file: {valuesFilePath}");
                    var valueLines = await System.IO.File.ReadAllLinesAsync(valuesFilePath);

                    foreach (var line in valueLines)
                    {
                        using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                            writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Processing value line: " + line.ToString());
                        Console.WriteLine($"Processing value line: {line}");
                        var parts = line.Split("//");
                        if (parts.Length != 5)
                        {
                            using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                                writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Skipping invalid value line: " + line.ToString());
                            Console.WriteLine($"Skipping invalid value line: {line}");
                            continue;
                        }

                        if (!int.TryParse(parts[0], out int step))
                        {
                            using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                                writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Invalid step value in line: " + line.ToString());
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

                        using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                            writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] " + $"Adding AlgoStep: AlgoId={algoStep.AlgoId}, Step={algoStep.Step}, VarName={algoStep.VarName}, Value={algoStep.Value}");
                        Console.WriteLine($"Adding AlgoStep: AlgoId={algoStep.AlgoId}, Step={algoStep.Step}, VarName={algoStep.VarName}, Value={algoStep.Value}");
                        _dbContext.AlgoSteps.Add(algoStep);
                    }

                    using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                        writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Saving AlgoSteps to database...");
                    Console.WriteLine("Saving AlgoSteps to database...");
                    await _dbContext.SaveChangesAsync();
                    using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                        writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] AlgoSteps saved successfully.");
                    Console.WriteLine("AlgoSteps saved successfully.");
                }
                else
                {
                    using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                        writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] Values file not found: " + valuesFilePath.ToString());
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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][Modify Subsystem] " + $"ModifyCode exception: {errorMessage}\nStackTrace: {ex.StackTrace}");
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
            string sourceFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");

            if (!System.IO.File.Exists(sourceFilePath))
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Execute Subsystem] Code file with ID {codeId} not found.");
                return NotFound($"Code file with ID {codeId} not found.");
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var codeModel = await _interpreterService.ExecuteCodeAsync(sourceFilePath);
                stopwatch.Stop();

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Execute Subsystem] ExecuteCode result: CodeId={codeModel.CodeId}, IsSuccessful={codeModel.IsSuccessful}, ErrorOutput={codeModel.ErrorOutput}");

                var errorsPath = Path.Combine(StorageDirectory, $"{codeId}errors.txt");
                var warningsPath = Path.Combine(StorageDirectory, $"{codeId}warnings.txt");
                var outputPath = Path.Combine(StorageDirectory, $"{codeId}output.txt");

                // Перезаписываем файлы
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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Execute Subsystem] ExecuteCode exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetValuesFile error: " + $"Values file not found for id: " + codeId.ToString());

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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetWarningsFile error: " + $"Warnings file not found for id: " + codeId.ToString());

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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetErrorsFile error: " + $"Errors file not found for id: " + codeId.ToString());

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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetOutputFile error: " + $"Output file no found for id: " + codeId.ToString());

                return NotFound("Output file not found.");
            }

            return PhysicalFile(outputFilePath, "text/plain", Path.GetFileName(outputFilePath));
        }

        [HttpPut("update/{codeId}")]
        public async Task<IActionResult> UpdateCode(int codeId, [FromBody] string newSourceCode)
        {
            if (string.IsNullOrWhiteSpace(newSourceCode))
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Subsystem] New source code for ID {codeId} is empty or null.");
                return BadRequest("New source code cannot be empty.");
            }

            string sourceFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");

            try
            {
                // Перезаписываем исходный файл
                await System.IO.File.WriteAllTextAsync(sourceFilePath, newSourceCode);

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Subsystem] Source code for ID {codeId} updated successfully.");

                return Ok($"Source code for ID {codeId} updated successfully.");
            }
            catch (Exception ex)
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Subsystem] UpdateCode exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Failed to update source code: {ex.Message}");
            }
        }

        [HttpPut("update-metadata/{codeId}")]
        public async Task<IActionResult> UpdateMetadata(int codeId, [FromBody] string newMetadata)
        {
            if (string.IsNullOrWhiteSpace(newMetadata))
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Metadata Subsystem] New metadata for ID {codeId} is empty or null.");
                return BadRequest("New metadata cannot be empty.");
            }

            string metadataFilePath = Path.Combine(StorageDirectory, $"{codeId}init.txt");

            try
            {
                // Перезаписываем метафайл
                await System.IO.File.WriteAllTextAsync(metadataFilePath, newMetadata);

                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Metadata Subsystem] Metadata for ID {codeId} updated successfully.");

                return Ok($"Metadata for ID {codeId} updated successfully.");
            }
            catch (Exception ex)
            {
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine($"[{DateTime.Now}][Update Metadata Subsystem] UpdateMetadata exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Failed to update metadata: {ex.Message}");
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
                using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                    writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetVariables error: " + $"GetVariables exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                Console.WriteLine($"GetVariables exception: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }


        [HttpGet("report/{id}")]
        public async Task<IActionResult> GetReport(int id)
        {
            var resultFilePath = $"/app/code_files/{id}result.txt";
            var errorsFilePath = $"/app/code_files/{id}errors.txt";
            var warningsFilePath = $"/app/code_files/{id}warnings.txt";
            var outputFilePath = $"/app/code_files/{id}output.txt";
            var initFilePath = $"/app/code_files/{id}init.txt";
            var sourceFilePath = $"/app/code_files/{id}.cs";
            var valuesFilePath = $"/app/code_files/{id}values.txt";
            var modifiedErrorsFilePath = $"/app/code_files/{id}modifiederrors.txt";
            var modifiedWarningsFilePath = $"/app/code_files/{id}modifiedwarnings.txt";

            var report = new StringBuilder();
            bool algorithmExecuted = true;

            // Проверка ошибок компиляции исходного кода
            string errorsContent = string.Empty;
            if (System.IO.File.Exists(errorsFilePath))
            {
                errorsContent = await System.IO.File.ReadAllTextAsync(errorsFilePath);
                if (!string.IsNullOrWhiteSpace(errorsContent))
                {
                    algorithmExecuted = false;
                }
            }

            // Проверка ошибок компиляции модифицированного кода
            string modifiedErrorsContent = string.Empty;
            if (System.IO.File.Exists(modifiedErrorsFilePath))
            {
                modifiedErrorsContent = await System.IO.File.ReadAllTextAsync(modifiedErrorsFilePath);
                if (!string.IsNullOrWhiteSpace(modifiedErrorsContent))
                {
                    algorithmExecuted = false;
                }
            }

            // Заголовок отчёта
            report.AppendLine($"Алгоритм {id} {(algorithmExecuted ? "выполнен" : "не выполнен")}");

            // Ошибки компиляции исходного кода
            if (!string.IsNullOrWhiteSpace(errorsContent))
            {
                report.AppendLine("\nСписок ошибок при компиляции исходного кода:");
                report.AppendLine(errorsContent);
            }
            else if (System.IO.File.Exists(errorsFilePath))
            {
                report.AppendLine("\nОшибок при компиляции исходного кода нет.");
            }
            else
            {
                report.AppendLine("\nФайл ошибок компиляции исходного кода не найден.");
            }

            // Ошибки компиляции модифицированного кода
            if (!string.IsNullOrWhiteSpace(modifiedErrorsContent))
            {
                report.AppendLine("\nСписок ошибок при компиляции модифицированного кода:");
                report.AppendLine(modifiedErrorsContent);
            }
            else if (System.IO.File.Exists(modifiedErrorsFilePath))
            {
                report.AppendLine("\nОшибок при компиляции модифицированного кода нет.");
            }
            else
            {
                report.AppendLine("\nФайл ошибок компиляции модифицированного кода не найден.");
            }

            // Предупреждения компиляции исходного кода
            if (System.IO.File.Exists(warningsFilePath))
            {
                var warningsContent = await System.IO.File.ReadAllTextAsync(warningsFilePath);
                report.AppendLine("\nСписок предупреждений при компиляции исходного кода:");
                report.AppendLine(string.IsNullOrWhiteSpace(warningsContent) ? "Предупреждений нет." : warningsContent);
            }
            else
            {
                report.AppendLine("\nФайл предупреждений компиляции исходного кода не найден.");
            }

            // Предупреждения компиляции модифицированного кода
            if (System.IO.File.Exists(modifiedWarningsFilePath))
            {
                var modifiedWarningsContent = await System.IO.File.ReadAllTextAsync(modifiedWarningsFilePath);
                report.AppendLine("\nСписок предупреждений при компиляции модифицированного кода:");
                report.AppendLine(string.IsNullOrWhiteSpace(modifiedWarningsContent) ? "Предупреждений нет." : modifiedWarningsContent);
            }
            else
            {
                report.AppendLine("\nФайл предупреждений компиляции модифицированного кода не найден.");
            }

            // Вывод алгоритма
            if (algorithmExecuted && System.IO.File.Exists(outputFilePath))
            {
                var outputContent = await System.IO.File.ReadAllTextAsync(outputFilePath);
                report.AppendLine("\nВывод алгоритма:");
                report.AppendLine(string.IsNullOrWhiteSpace(outputContent) ? "Вывод отсутствует." : outputContent);
            }
            else if (algorithmExecuted)
            {
                report.AppendLine("\nФайл вывода алгоритма не найден.");
            }

            // Метаданные
            if (System.IO.File.Exists(initFilePath))
            {
                var initContent = await System.IO.File.ReadAllTextAsync(initFilePath);
                report.AppendLine("\nСодержимое файла метаданных:");
                report.AppendLine(string.IsNullOrWhiteSpace(initContent) ? "Файл пуст." : initContent);
            }
            else
            {
                report.AppendLine("\nФайл метаданных (init.txt) не найден.");
            }

            // Исходный код
            if (System.IO.File.Exists(sourceFilePath))
            {
                var sourceContent = await System.IO.File.ReadAllTextAsync(sourceFilePath);
                report.AppendLine("\nСодержимое исходного файла алгоритма:");
                report.AppendLine(string.IsNullOrWhiteSpace(sourceContent) ? "Файл пуст." : sourceContent);
            }
            else
            {
                report.AppendLine("\nИсходный файл алгоритма не найден.");
            }

            // Значения переменных
            if (System.IO.File.Exists(valuesFilePath))
            {
                var valuesContent = await System.IO.File.ReadAllTextAsync(valuesFilePath);
                report.AppendLine("\nСодержимое файла значений переменных:");
                report.AppendLine(string.IsNullOrWhiteSpace(valuesContent) ? "Файл пуст." : valuesContent);
            }
            else
            {
                report.AppendLine("\nФайл значений переменных не найден.");
            }

            // Сохраняем отчёт
            await System.IO.File.WriteAllTextAsync(resultFilePath, report.ToString());

            // Возвращаем файл отчёта
            if (System.IO.File.Exists(resultFilePath))
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(resultFilePath);
                return File(fileBytes, "text/plain", $"{id}result.txt");
            }
            using (StreamWriter writer = new StreamWriter("/app/code_files/logs.txt", append: true))
                writer.WriteLine("[" + DateTime.Now.ToString() + "][General] GetReport error: " + $"Report File {id}result.txt was not created.");
            return NotFound($"Файл отчёта {id}result.txt не создан.");
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

        [HttpPost("variable-types/{codeId}")]
        public async Task<IActionResult> GetVariableTypes(int codeId, IFormFile metadataFile)
        {
            try
            {
                var codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
                if (!System.IO.File.Exists(codeFilePath))
                    return NotFound($"Code file with ID {codeId} not found.");

                var codeLines = await System.IO.File.ReadAllLinesAsync(codeFilePath);

                using var streamReader = new StreamReader(metadataFile.OpenReadStream());
                var metadata = await streamReader.ReadToEndAsync();
                var trackLines = metadata.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2 || !int.TryParse(parts[0], out int lineNum))
                            return (LineNumber: 0, VariableNames: Array.Empty<string>());
                        return (LineNumber: lineNum, VariableNames: parts.Skip(1).ToArray());
                    })
                    .Where(t => t.LineNumber > 0)
                    .ToList();

                var variableTypes = new List<object>();
                var typeMap = new Dictionary<string, (string Type, int Rank)>();

                for (int i = 0; i < codeLines.Length; i++)
                {
                    var line = codeLines[i].Trim();
                    if (line.Contains("=") && !line.StartsWith("//"))
                    {
                        var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            var decl = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (decl.Length >= 2)
                            {
                                var type = decl[0];
                                var name = decl[1].TrimEnd(';');
                                int rank = type.EndsWith("[]") ? 1 : type.EndsWith("[,]") ? 2 : 0;
                                type = type.Replace("[]", string.Empty).Replace("[,]", string.Empty);
                                typeMap[name] = (type, rank);
                            }
                        }
                    }
                }

                foreach (var (lineNumber, variables) in trackLines)
                {
                    if (lineNumber <= codeLines.Length)
                    {
                        foreach (var varName in variables)
                        {
                            if (typeMap.TryGetValue(varName, out var typeInfo))
                            {
                                var typeName = typeInfo.Type + (typeInfo.Rank == 1 ? "[]" : typeInfo.Rank == 2 ? "[,]" : "");
                                variableTypes.Add(new
                                {
                                    LineNumber = lineNumber,
                                    VariableName = varName,
                                    Type = typeName
                                });
                            }
                        }
                    }
                }

                return Ok(variableTypes);
            }
            catch (Exception ex)
            {
                await System.IO.File.AppendAllTextAsync(Path.Combine(StorageDirectory, "logs.txt"),
                    $"[{DateTime.Now}][VariableTypes] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }


        [HttpPost("update-values/{codeId}")]
        public async Task<IActionResult> UpdateVariableValues(int codeId, [FromBody] List<VariableUpdateDto> updates)
        {
            try
            {
                var codeFilePath = Path.Combine(StorageDirectory, $"{codeId}.cs");
                if (!System.IO.File.Exists(codeFilePath))
                    return NotFound($"Code file with ID {codeId} not found.");

                var codeLines = await System.IO.File.ReadAllLinesAsync(codeFilePath);
                var typeMap = new Dictionary<string, (string Type, int Rank, int Line, int LineCount)>();

                // Определяем типы переменных и количество строк для инициализации
                for (int i = 0; i < codeLines.Length; i++)
                {
                    var line = codeLines[i].Trim();
                    if (line.Contains("=") && !line.StartsWith("//"))
                    {
                        var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            var decl = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (decl.Length >= 2)
                            {
                                var type = decl[0];
                                var name = decl[1].TrimEnd(';');
                                int rank = type.EndsWith("[]") ? 1 : type.EndsWith("[,]") ? 2 : 0;
                                type = type.Replace("[]", string.Empty).Replace("[,]", string.Empty);
                                int lineCount = 1; // По умолчанию одна строка для скалярных типов

                                if (rank > 0) // Для массивов определяем количество строк
                                {
                                    int j = i;
                                    int braceCount = codeLines[i].Count(c => c == '{') - codeLines[i].Count(c => c == '}');
                                    bool foundSemicolon = codeLines[i].Trim().EndsWith(";");
                                    while (j < codeLines.Length && (!foundSemicolon || braceCount > 0))
                                    {
                                        j++;
                                        if (j < codeLines.Length)
                                        {
                                            braceCount += codeLines[j].Count(c => c == '{') - codeLines[j].Count(c => c == '}');
                                            foundSemicolon |= codeLines[j].Trim().EndsWith(";");
                                        }
                                    }
                                    lineCount = j - i + (foundSemicolon ? 1 : 0);
                                }

                                typeMap[name] = (type, rank, i + 1, lineCount);
                            }
                        }
                    }
                }

                // Копируем строки для модификации
                var newLines = codeLines.ToList();
                foreach (var updateGroup in updates.GroupBy(u => u.LineNumber))
                {
                    int lineNumber = updateGroup.Key;
                    if (lineNumber > codeLines.Length)
                        return BadRequest($"Line {lineNumber} is out of range.");

                    foreach (var update in updateGroup)
                    {
                        if (!typeMap.TryGetValue(update.VariableName, out var typeInfo))
                            return BadRequest($"Variable {update.VariableName} not found in code.");

                        if (typeInfo.Line != lineNumber)
                            return BadRequest($"Variable {update.VariableName} is not assigned at line {lineNumber}.");

                        string newValue;
                        if (typeInfo.Rank == 0)
                        {
                            if (typeInfo.Type == "string")
                                newValue = $"\"{update.Value}\"";
                            else if (typeInfo.Type == "char")
                                newValue = $"'{update.Value}'";
                            else
                                newValue = update.Value;
                        }
                        else if (typeInfo.Rank == 1)
                        {
                            newValue = $"{{ {update.Value} }}";
                        }
                        else
                        {
                            var rows = updateGroup.Where(u => u.VariableName == update.VariableName)
                                .Select(u => u.Value).ToList();
                            newValue = $"{{ {{ {string.Join("}, {", rows)} }} }}";
                        }

                        var originalLine = codeLines[typeInfo.Line - 1];
                        var assignIndex = originalLine.IndexOf('=');
                        if (assignIndex < 0)
                            return BadRequest($"No assignment found for {update.VariableName} at line {lineNumber}.");

                        // Заменяем первую строку инициализации новым значением
                        newLines[typeInfo.Line - 1] = originalLine.Substring(0, assignIndex + 1) + $" {newValue};";

                        // Оставляем последующие строки пустыми, если инициализация занимала несколько строк
                        for (int i = 1; i < typeInfo.LineCount; i++)
                        {
                            if (typeInfo.Line - 1 + i < newLines.Count)
                                newLines[typeInfo.Line - 1 + i] = "";
                        }
                    }
                }

                // Убедимся, что количество строк совпадает с исходным файлом
                while (newLines.Count < codeLines.Length)
                    newLines.Add("");

                int testId = new Random().Next(100000, 999999);
                var testFilePath = Path.Combine(StorageDirectory, $"test{codeId}_{testId}.cs");
                await System.IO.File.WriteAllTextAsync(testFilePath, string.Join("\n", newLines));

                return Ok(new { TestId = testId });
            }
            catch (Exception ex)
            {
                await System.IO.File.AppendAllTextAsync(Path.Combine(StorageDirectory, "logs.txt"),
                    $"[{DateTime.Now}][UpdateValues] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }


        [HttpPost("execute-test/{codeId}/{testId}")]
        public async Task<IActionResult> ExecuteTest(int codeId, int testId)
        {
            try
            {
                var testFilePath = Path.Combine(StorageDirectory, $"test{codeId}_{testId}.cs");
                if (!System.IO.File.Exists(testFilePath))
                    return NotFound($"Test file test{codeId}_{testId}.cs not found.");

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
                var executionTask = _interpreterService.ExecuteCodeAsync(testFilePath);
                var completedTask = await Task.WhenAny(executionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    return StatusCode(500, new
                    {
                        IsSuccessful = false,
                        Output = "",
                        Error = "Execution timed out after 15 seconds, possible infinite loop.",
                        ExecutionTime = 15000
                    });
                }

                var stopwatch = Stopwatch.StartNew();
                var codeModel = await executionTask;
                stopwatch.Stop();

                var errorsPath = Path.Combine(StorageDirectory, $"{codeId}_{testId}_errors.txt");
                var warningsPath = Path.Combine(StorageDirectory, $"{codeId}_{testId}_warnings.txt");
                var outputPath = Path.Combine(StorageDirectory, $"{codeId}_{testId}_output.txt");

                await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                await System.IO.File.WriteAllTextAsync(warningsPath, codeModel.WarningOutput ?? "");
                await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                return Ok(new
                {
                    IsSuccessful = codeModel.IsSuccessful,
                    Output = codeModel.StandardOutput ?? "",
                    Error = codeModel.ErrorOutput ?? "",
                    ExecutionTime = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex)
            {
                await System.IO.File.AppendAllTextAsync(Path.Combine(StorageDirectory, "logs.txt"),
                    $"[{DateTime.Now}][ExecuteTest] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }

        [HttpPost("substitute-values/{codeId}/{testId}")]
        public async Task<IActionResult> SubstituteValues(int codeId, int testId, IFormFile userDataFile)
        {
            try
            {
                var testFilePath = System.IO.Path.Combine(StorageDirectory, $"test{codeId}_{testId}_modified.cs");
                if (!System.IO.File.Exists(testFilePath))
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Test file not found: {testFilePath}\n");
                    return NotFound($"Test file test{codeId}_{testId}_modified.cs not found.");
                }

                var userDataPath = System.IO.Path.Combine(StorageDirectory, $"{codeId}_{testId}_userdata.txt");
                using (var stream = new System.IO.FileStream(userDataPath, System.IO.FileMode.Create))
                    await userDataFile.CopyToAsync(stream);

                var userDataLines = await System.IO.File.ReadAllLinesAsync(userDataPath);
                if (userDataLines.Length < 1)
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] User data file is empty\n");
                    return BadRequest("User data file is empty.");
                }

                var header = userDataLines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (header.Length != 2 || !int.TryParse(header[0], out int userId) || !int.TryParse(header[1], out int testTaskId))
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Invalid user data header: {string.Join(" ", header)}\n");
                    return BadRequest("Invalid user data header.");
                }

                await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                    $"[{DateTime.Now}][SubstituteValues] User data file content:\n{string.Join("\n", userDataLines)}\n");

                // Создание файлов values.txt и mismatches.txt в начале метода
                var txtValuesPath = System.IO.Path.Combine(StorageDirectory, $"{codeId}_{testId}values.txt");
                var txtMismatchesPath = System.IO.Path.Combine(StorageDirectory, $"{codeId}_{testId}mismatches.txt");
                try
                {
                    await System.IO.File.WriteAllTextAsync(txtValuesPath, string.Empty);
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Created/Cleared values file: {txtValuesPath}\n");

                    await System.IO.File.WriteAllTextAsync(txtMismatchesPath, string.Empty);
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Created/Cleared mismatches file: {txtMismatchesPath}\n");
                }
                catch (Exception ex)
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Error creating/clearing files: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                    return StatusCode(500, new CodeExecutionResponse
                    {
                        IsSuccessful = false,
                        Output = "",
                        Error = $"Failed to create/clear values/mismatches files: {ex.Message}",
                        Values = new List<TestVariableTracker.ValueData>(),
                        Mismatches = new List<TestVariableTracker.MismatchData>(),
                        ExecutionTime = 0
                    });
                }

                var modifiedCode = await System.IO.File.ReadAllTextAsync(testFilePath);
                var initializeLine = $"await TestVariableTracker.Initialize(\"{codeId}_{testId}\", \"{userDataPath}\");";
                var lines = modifiedCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
                int mainIndex = lines.FindIndex(l => l.Contains("static void Main") || l.Contains("static async Task Main"));
                if (mainIndex >= 0)
                {
                    lines[mainIndex] = lines[mainIndex].Replace("static void Main", "static async Task Main");
                    int insertIndex = lines.FindIndex(mainIndex, l => l.Trim().StartsWith("{"));
                    if (insertIndex >= 0)
                    {
                        lines.Insert(insertIndex + 1, new string(' ', lines[insertIndex].TakeWhile(char.IsWhiteSpace).Count()) + initializeLine);
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Inserted Initialize call at line {insertIndex + 2}\n");
                    }
                    else
                    {
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Failed to find opening brace in Main method\n");
                    }
                }
                else
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Main method not found in test file\n");
                }

                var tempFilePath = System.IO.Path.Combine(StorageDirectory, $"temp_{codeId}_{testId}.cs");
                await System.IO.File.WriteAllTextAsync(tempFilePath, string.Join("\n", lines));
                await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                    $"[{DateTime.Now}][SubstituteValues] Temporary file created: {tempFilePath}\n");

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
                var executionTask = _interpreterService.ExecuteCodeAsync(tempFilePath);
                var completedTask = await Task.WhenAny(executionTask, timeoutTask);

                try
                {
                    var values = new List<TestVariableTracker.ValueData>();
                    var mismatches = new List<TestVariableTracker.MismatchData>();
                    string errorMessage = null;
                    var stopwatch = Stopwatch.StartNew();
                    var codeModel = new CodeModel(codeId, tempFilePath)
                    {
                        IsSuccessful = false,
                        StandardOutput = "",
                        ErrorOutput = ""
                    };

                    if (completedTask == timeoutTask)
                    {
                        errorMessage = "Execution timed out after 15 seconds, possible infinite loop.";
                        stopwatch.Stop();
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Execution timed out\n");
                    }
                    else
                    {
                        codeModel = await executionTask;
                        stopwatch.Stop();
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Execution completed: IsSuccessful={codeModel.IsSuccessful}, StandardOutput={codeModel.StandardOutput}, ErrorOutput={codeModel.ErrorOutput}\n");
                    }

                    // Читаем TXT-файлы
                    if (System.IO.File.Exists(txtValuesPath))
                    {
                        try
                        {
                            var txtLines = await System.IO.File.ReadAllLinesAsync(txtValuesPath);
                            values = txtLines.Select(line =>
                            {
                                var parts = line.Split("//");
                                if (parts.Length < 6) return null;
                                return new TestVariableTracker.ValueData
                                {
                                    LineNumber = int.TryParse(parts[0], out int lineNum) ? lineNum : 0,
                                    TrackerHitId = int.TryParse(parts[1], out int methodId) ? methodId : 0,
                                    VariableName = parts[2],
                                    Type = parts[3],
                                    Rank = int.TryParse(parts[4], out int rank) ? rank : 0,
                                    Value = parts[5]
                                };
                            }).Where(v => v != null).ToList();
                            await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                                $"[{DateTime.Now}][SubstituteValues] Read values TXT: {values.Count} items\n{string.Join("\n", txtLines)}\n");
                        }
                        catch (Exception ex)
                        {
                            await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                                $"[{DateTime.Now}][SubstituteValues] Error reading values TXT: {ex.Message}\n");
                        }
                    }
                    else
                    {
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Values TXT file not found: {txtValuesPath}\n");
                    }

                    if (System.IO.File.Exists(txtMismatchesPath))
                    {
                        try
                        {
                            var txtLines = await System.IO.File.ReadAllLinesAsync(txtMismatchesPath);
                            mismatches = txtLines.Select(line =>
                            {
                                var parts = line.Split("//");
                                if (parts.Length < 5) return null;
                                return new TestVariableTracker.MismatchData
                                {
                                    TrackerHitId = int.TryParse(parts[0], out int methodId) ? methodId : 0,
                                    LineNumber = int.TryParse(parts[1], out int lineNum) ? lineNum : 0,
                                    VariableName = parts[2],
                                    ActualValue = parts[3],
                                    ExpectedValue = parts[4]
                                };
                            }).Where(m => m != null).ToList();
                            await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                                $"[{DateTime.Now}][SubstituteValues] Read mismatches TXT: {mismatches.Count} items\n{string.Join("\n", txtLines)}\n");
                        }
                        catch (Exception ex)
                        {
                            await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                                $"[{DateTime.Now}][SubstituteValues] Error reading mismatches TXT: {ex.Message}\n");
                        }
                    }
                    else
                    {
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                            $"[{DateTime.Now}][SubstituteValues] Mismatches TXT file not found: {txtMismatchesPath}\n");
                    }

                    var errorsPath = System.IO.Path.Combine(StorageDirectory, $"{codeId}_{testId}_errors.txt");
                    var outputPath = System.IO.Path.Combine(StorageDirectory, $"{codeId}_{testId}_output.txt");
                    await System.IO.File.WriteAllTextAsync(errorsPath, codeModel.ErrorOutput ?? "");
                    await System.IO.File.WriteAllTextAsync(outputPath, codeModel.StandardOutput ?? "");

                    var response = new CodeExecutionResponse
                    {
                        IsSuccessful = codeModel.IsSuccessful && errorMessage == null,
                        Output = codeModel.StandardOutput ?? "",
                        Error = errorMessage ?? (codeModel.IsSuccessful ? "" : codeModel.ErrorOutput ?? ""),
                        Values = values,
                        Mismatches = mismatches,
                        ExecutionTime = stopwatch.ElapsedMilliseconds
                    };

                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Response: IsSuccessful={response.IsSuccessful}, ValuesCount={response.Values.Count}, MismatchesCount={response.Mismatches.Count}, Error={response.Error}, ExecutionTime={response.ExecutionTime}\n");

                    return Ok(response);
                }
                finally
                {
                    await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                        $"[{DateTime.Now}][SubstituteValues] Temporary files preserved: {tempFilePath}, {txtValuesPath}, {txtMismatchesPath}\n");
                }
            }
            catch (Exception ex)
            {
                await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "debug.txt"),
                    $"[{DateTime.Now}][SubstituteValues] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                return StatusCode(500, new CodeExecutionResponse
                {
                    IsSuccessful = false,
                    Output = "",
                    Error = $"Server error: {ex.Message}",
                    Values = new List<TestVariableTracker.ValueData>(),
                    Mismatches = new List<TestVariableTracker.MismatchData>(),
                    ExecutionTime = 0
                });
            }
        }



        // Структура для хранения данных о точке трекинга
        private struct TrackingPoint
        {
            public int LineNumber { get; set; }
            public int MethodId { get; set; }
            public string[] VariableNames { get; set; }
        }

        [HttpPost("modify-test/{codeId}/{testId}")]
        public async Task<IActionResult> ModifyTest(int codeId, int testId, IFormFile metadataFile)
        {
            try
            {
                var testFilePath = System.IO.Path.Combine(StorageDirectory, $"test{codeId}_{testId}.cs");
                if (!System.IO.File.Exists(testFilePath))
                    return NotFound($"Test file test{codeId}_{testId}.cs not found.");

                using var streamReader = new StreamReader(metadataFile.OpenReadStream());
                var metadata = await streamReader.ReadToEndAsync();
                var trackLines = metadata.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line =>
                    {
                        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2 || !int.TryParse(parts[0], out int lineNum))
                            return new TrackingPoint { LineNumber = 0, MethodId = 0, VariableNames = Array.Empty<string>() };

                        int methodId = lineNum;
                        if (parts.Length > 2 && int.TryParse(parts[1], out int parsedMethodId))
                        {
                            methodId = parsedMethodId;
                            return new TrackingPoint
                            {
                                LineNumber = lineNum,
                                MethodId = methodId,
                                VariableNames = parts.Skip(2).ToArray()
                            };
                        }
                        return new TrackingPoint
                        {
                            LineNumber = lineNum,
                            MethodId = methodId,
                            VariableNames = parts.Skip(1).ToArray()
                        };
                    })
                    .Where(t => t.LineNumber > 0)
                    .ToList();

                var code = await System.IO.File.ReadAllTextAsync(testFilePath);
                var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

                var variableTypes = new Dictionary<string, string>();
                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Contains("=") && !line.StartsWith("//"))
                    {
                        var parts = line.Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            var decl = parts[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (decl.Length >= 2)
                            {
                                var type = decl[0];
                                var name = decl[1].TrimEnd(';');
                                variableTypes[name] = type;
                            }
                        }
                    }
                    if (line.StartsWith("for") && line.Contains("int "))
                    {
                        var decl = line.Substring(line.IndexOf("int ") + 4).Split('=', StringSplitOptions.RemoveEmptyEntries);
                        if (decl.Length > 0)
                        {
                            var name = decl[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            variableTypes[name] = "int";
                        }
                    }
                }

                var controlStructures = new HashSet<string> { "for", "while", "if", "else", "switch", "do", "foreach", "try", "catch", "finally" };
                var insertions = new List<(int InsertAfterLine, string TrackerLine)>();
                int uniqueId = 0;

                foreach (var point in trackLines.OrderByDescending(t => t.LineNumber))
                {
                    int lineNumber = point.LineNumber;
                    int methodId = point.MethodId;
                    string[] variables = point.VariableNames;

                    if (lineNumber < 1 || lineNumber > lines.Count)
                    {
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "logs.txt"),
                            $"[{DateTime.Now}][ModifyTest] Invalid line number {lineNumber} for methodId {methodId}\n");
                        continue;
                    }

                    int insertLine = lineNumber;
                    string prevLine = lineNumber > 1 ? lines[lineNumber - 2].Trim() : "";
                    string currentLine = lines[lineNumber - 1].Trim();
                    string nextLine = lineNumber < lines.Count ? lines[lineNumber].Trim() : "";

                    bool isControlStructure = controlStructures.Any(cs => prevLine.StartsWith(cs) || prevLine.Contains($"{cs} ") || currentLine.StartsWith(cs));
                    bool hasOpeningBrace = nextLine.StartsWith("{") || currentLine.EndsWith("{");
                    bool hasSemicolon = currentLine.EndsWith(";");

                    if (isControlStructure && hasOpeningBrace)
                    {
                        // Ищем конец блока
                        int braceCount = 1;
                        insertLine = lineNumber + 1;
                        while (insertLine < lines.Count && braceCount > 0)
                        {
                            var line = lines[insertLine - 1].Trim();
                            braceCount += line.Count(c => c == '{') - line.Count(c => c == '}');
                            insertLine++;
                        }
                        insertLine--; // Вставляем перед закрывающей скобкой
                    }
                    else if (isControlStructure && !hasOpeningBrace && !hasSemicolon)
                    {
                        // Ищем конец однострочного блока
                        insertLine = lineNumber + 1;
                        while (insertLine <= lines.Count && lines[insertLine - 1].Trim() == "")
                            insertLine++;
                    }
                    else if (!hasSemicolon && !hasOpeningBrace)
                    {
                        // Пропускаем, если строка не завершена
                        await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "logs.txt"),
                            $"[{DateTime.Now}][ModifyTest] Skipping line {lineNumber} for methodId {methodId}: incomplete statement\n");
                        continue;
                    }

                    if (insertLine <= lines.Count)
                    {
                        var trackArgs = string.Join(", ", variables.Select(v => $"(\"{v}\", {v})"));
                        var indent = new string(' ', lines[lineNumber - 1].TakeWhile(char.IsWhiteSpace).Count());
                        var updatesVar = $"updates_{methodId}_{uniqueId++}";
                        var trackerLine = $"{indent}var {updatesVar} = await TestVariableTracker.TrackVariables({methodId}, {lineNumber}, {trackArgs});";

                        var updateLines = variables.Select(v =>
                        {
                            string type = variableTypes.ContainsKey(v) ? variableTypes[v] : "object";
                            string castType = type switch
                            {
                                "int[]" => "(int[])",
                                "float[]" => "(float[])",
                                "double[]" => "(double[])",
                                "int[,]" => "(int[,])",
                                "float[,]" => "(float[,])",
                                "double[,]" => "(double[,])",
                                "int" => "(int)",
                                "float" => "(float)",
                                "double" => "(double)",
                                "char" => "(char)",
                                "string" => "(string)",
                                _ => ""
                            };
                            return $"{indent}if ({updatesVar}.ContainsKey(\"{v}\")) {v} = {castType}{updatesVar}[\"{v}\"];";
                        }).ToList();

                        insertions.Add((insertLine, string.Join("\n", new[] { trackerLine }.Concat(updateLines))));
                    }
                }

                foreach (var (insertLine, trackerLine) in insertions.OrderByDescending(x => x.InsertAfterLine))
                {
                    lines.InsertRange(insertLine, trackerLine.Split('\n'));
                }

                var usings = new List<string>();
                if (!code.Contains("using System.Linq;"))
                    usings.Add("using System.Linq;");
                if (!code.Contains("using System.IO;"))
                    usings.Add("using System.IO;");
                if (!code.Contains("using System.Collections.Generic;"))
                    usings.Add("using System.Collections.Generic;");
                if (!code.Contains("using System.Threading.Tasks;"))
                    usings.Add("using System.Threading.Tasks;");

                if (usings.Any())
                {
                    int insertIndex = lines.FindIndex(l => !l.Trim().StartsWith("using "));
                    lines.InsertRange(insertIndex, usings);
                }

                var trackerCode = TestVariableTracker.GetTrackerMethodCode($"{codeId}_{testId}");
                var trackerLines = trackerCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                    .Where(line => !line.Trim().StartsWith("using "))
                    .ToList();
                lines.Add("\n");
                lines.AddRange(trackerLines);

                var modifiedFilePath = System.IO.Path.Combine(StorageDirectory, $"test{codeId}_{testId}_modified.cs");
                await System.IO.File.WriteAllTextAsync(modifiedFilePath, string.Join("\n", lines));

                return Ok("Test file modified successfully.");
            }
            catch (Exception ex)
            {
                await System.IO.File.AppendAllTextAsync(System.IO.Path.Combine(StorageDirectory, "logs.txt"),
                    $"[{DateTime.Now}][ModifyTest] Exception: {ex.Message}\nStackTrace: {ex.StackTrace}\n");
                return StatusCode(500, $"Server error: {ex.Message}");
            }
        }



        private string ModifyCode(string code, List<(int LineNumber, string[] VariableNames)> trackLines, int codeId)
        {
            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            var controlStructures = new HashSet<string> { "for", "while", "if", "else", "switch", "do", "foreach", "try", "catch", "finally" };
            var insertions = new List<(int InsertAfterLine, string TrackerLine)>();

            foreach (var (lineNumber, variables) in trackLines.OrderByDescending(t => t.LineNumber))
            {
                if (lineNumber < 1 || lineNumber >= lines.Count)
                    continue;

                int insertLine = lineNumber;
                string prevLine = lineNumber > 0 ? lines[lineNumber - 1].Trim() : "";
                string currentLine = lines[lineNumber - 1].Trim();
                string nextLine = lineNumber < lines.Count ? lines[lineNumber].Trim() : "";

                bool isControlStructure = controlStructures.Any(cs => prevLine.StartsWith(cs) || prevLine.Contains($"{cs} ") || currentLine.StartsWith(cs));
                bool hasOpeningBrace = nextLine.StartsWith("{") || currentLine.EndsWith("{");

                if (isControlStructure && hasOpeningBrace)
                {
                    insertLine = lineNumber + 1;
                    while (insertLine < lines.Count && lines[insertLine - 1].Trim() == "")
                        insertLine++;
                }
                else if (isControlStructure && !hasOpeningBrace)
                {
                    insertLine = lineNumber;
                    while (insertLine < lines.Count && !lines[insertLine - 1].Trim().EndsWith(";") && !lines[insertLine - 1].Trim().EndsWith("{"))
                        insertLine++;
                }

                if (insertLine <= lines.Count)
                {
                    var trackArgs = string.Join(", ", variables.Select(v => $"(\"{v}\", {v})"));
                    var indent = new string(' ', lines[lineNumber - 1].TakeWhile(char.IsWhiteSpace).Count());
                    var trackerLine = $"{indent}var updates = VariableTracker.TrackVariables({insertions.Count + 1}, {trackArgs});";
                    var updateLines = variables.Select(v => $"{indent}if (updates.ContainsKey(\"{v}\")) {v} = updates[\"{v}\"];").ToList();
                    insertions.Add((insertLine, string.Join("\n", new[] { trackerLine }.Concat(updateLines))));
                }
            }

            foreach (var (insertLine, trackerLine) in insertions.OrderByDescending(x => x.InsertAfterLine))
            {
                lines.InsertRange(insertLine, trackerLine.Split('\n'));
            }

            var modifiedCode = string.Join("\n", lines);

            var usings = new List<string>();
            if (!modifiedCode.Contains("using System.Linq;"))
                usings.Add("using System.Linq;");
            if (!modifiedCode.Contains("using System.IO;"))
                usings.Add("using System.IO;");
            if (!modifiedCode.Contains("using System.Collections.Generic;"))
                usings.Add("using System.Collections.Generic;");
            if (!modifiedCode.Contains("using System.Threading.Tasks;"))
                usings.Add("using System.Threading.Tasks;");

            if (usings.Any())
                modifiedCode = string.Join("\n", usings) + "\n" + modifiedCode;

            string variableTrackerCode = TestVariableTracker.GetTrackerMethodCode(codeId.ToString());
            var variableTrackerLines = variableTrackerCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => !line.Trim().StartsWith("using "))
                .ToList();
            modifiedCode += "\n" + string.Join("\n", variableTrackerLines);

            return modifiedCode;
        }

    }
}