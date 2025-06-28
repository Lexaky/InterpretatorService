using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InterpretatorService.Data;
using InterpretatorService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using InterpretatorService.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;

namespace InterpretatorService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestManagementController : ControllerBase
{
    private readonly TestsDbContext _context;
    private readonly ILogger<TestManagementController> _logger;
    private const string StorageDirectory = "/app/code_files";

    public TestManagementController(TestsDbContext context, ILogger<TestManagementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("fetch-tests")]
    public async Task<IActionResult> FetchTests([FromQuery] int? algoId = null)
    {
        try
        {
            var query = _context.Tests.AsQueryable();
            if (algoId.HasValue)
            {
                query = query.Where(t => t.AlgoId == algoId.Value);
            }
            var tests = await query.ToListAsync();
            _logger.LogInformation($"Fetched {tests.Count} tests for algoId={algoId}");
            return Ok(tests);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching tests: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("fetch-test/{testId}")]
    public async Task<IActionResult> FetchTest(int testId)
    {
        try
        {
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }
            _logger.LogInformation($"Fetched test: testId={testId}");
            return Ok(test);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching test {testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("modify-test/{testId}")]
    public async Task<IActionResult> ModifyTest(int testId, [FromBody] UpdateTestDto update)
    {
        try
        {
            _logger.LogInformation($"Received modify-test request: testId={testId}, update={JsonSerializer.Serialize(update)}");
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            test.difficult = update.difficult;
            if (update.SolvedCount.HasValue)
            {
                test.SolvedCount = update.SolvedCount.Value;
            }
            if (update.UnsolvedCount.HasValue)
            {
                test.UnsolvedCount = update.UnsolvedCount.Value;
            }

            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated test: testId={testId}, difficult={test.difficult}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating test {testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("fetch-algo-steps/{algoId}")]
    public async Task<IActionResult> FetchAlgoSteps(int algoId)
    {
        try
        {
            var algoSteps = await _context.AlgoSteps
                .Where(a => a.AlgoId == algoId)
                .GroupBy(a => a.Step)
                .Select(g => g.OrderByDescending(a => a.AlgoId).First())
                .ToListAsync();
            _logger.LogInformation($"Fetched {algoSteps.Count} algo steps for algoId={algoId}");
            return Ok(algoSteps);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching algo steps for algoId={algoId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("modify-algo-step/{algoId}/{step}")]
    public async Task<IActionResult> ModifyAlgoStep(int algoId, int step, [FromBody] UpdateAlgoStepDto update)
    {
        try
        {
            if (update.AlgoId != algoId || update.Step != step)
            {
                _logger.LogWarning($"Mismatch in modify-algo-step request: URL algoId={algoId}, step={step}, body algoId={update.AlgoId}, step={update.Step}");
                return BadRequest("Mismatch between URL and body parameters.");
            }

            _logger.LogInformation($"Received modify-algo-step request: algoId={algoId}, step={step}, update={JsonSerializer.Serialize(update)}");
            var algoStep = await _context.AlgoSteps
                .FirstOrDefaultAsync(a => a.AlgoId == algoId && a.Step == step);
            if (algoStep == null)
            {
                algoStep = new AlgoStep
                {
                    AlgoId = algoId,
                    Step = step,
                    Difficult = update.Difficult
                };
                _context.AlgoSteps.Add(algoStep);
                _logger.LogInformation($"Created new algo step: algoId={algoId}, step={step}");
            }
            else
            {
                algoStep.Difficult = update.Difficult;
                _context.AlgoSteps.Update(algoStep);
                _logger.LogInformation($"Updated algo step: algoId={algoId}, step={step}, difficult={algoStep.Difficult}");
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating algo step for algoId={algoId}, step={step}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("fetch-step-responses/{testId}")]
    public async Task<IActionResult> FetchStepResponses(int testId)
    {
        try
        {
            var stepResponses = await _context.TestStepResponses
                .Where(tsr => tsr.TestId == testId)
                .ToListAsync();
            _logger.LogInformation($"Fetched {stepResponses.Count} step responses for testId={testId}");
            return Ok(stepResponses);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching step responses for testId={testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("modify-step-responses/{testId}")]
    public async Task<IActionResult> ModifyStepResponses(int testId, [FromBody] List<TestStepResponse> stepResponses)
    {
        try
        {
            _logger.LogInformation($"Received modify-step-responses request: testId={testId}, stepResponses={JsonSerializer.Serialize(stepResponses)}");
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            foreach (var stepResponse in stepResponses)
            {
                if (stepResponse.AlgoId != test.AlgoId)
                {
                    _logger.LogWarning($"AlgoId mismatch: testId={testId}, expected AlgoId={test.AlgoId}, received AlgoId={stepResponse.AlgoId}");
                    continue;
                }

                var existing = await _context.TestStepResponses
                    .FirstOrDefaultAsync(tsr => tsr.TestId == testId && tsr.AlgoStep == stepResponse.AlgoStep && tsr.AlgoId == test.AlgoId);
                if (existing == null)
                {
                    var newStepResponse = new TestStepResponse
                    {
                        TestId = testId,
                        AlgoId = test.AlgoId,
                        AlgoStep = stepResponse.AlgoStep,
                        CorrectCount = stepResponse.CorrectCount,
                        IncorrectCount = stepResponse.IncorrectCount
                    };
                    _context.TestStepResponses.Add(newStepResponse);
                    _logger.LogInformation($"Added new step response: testId={testId}, algoStep={stepResponse.AlgoStep}, correct={stepResponse.CorrectCount}, incorrect={stepResponse.IncorrectCount}");
                }
                else
                {
                    existing.CorrectCount += stepResponse.CorrectCount;
                    existing.IncorrectCount += stepResponse.IncorrectCount;
                    _context.TestStepResponses.Update(existing);
                    _logger.LogInformation($"Updated step response: testId={testId}, algoStep={stepResponse.AlgoStep}, correct={existing.CorrectCount}, incorrect={existing.IncorrectCount}");
                }
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated step responses for testId={testId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating step responses for testId={testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("fetch-test-details/{testId}")]
    public async Task<IActionResult> FetchTestDetails(int testId)
    {
        try
        {
            // Получение данных теста
            var test = await _context.Tests
                .FirstOrDefaultAsync(t => t.TestId == testId);

            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            // Получение данных алгоритма
            var algorithm = await _context.Algorithms
                .FirstOrDefaultAsync(a => a.AlgoId == test.AlgoId);

            if (algorithm == null)
            {
                _logger.LogWarning($"Algorithm not found for algoId={test.AlgoId}, testId={testId}");
                return NotFound($"Algorithm not found for testId={testId}");
            }

            // Получение входных данных теста
            var inputTestData = await _context.InputData
                .Where(itd => itd.TestId == testId)
                .ToListAsync();

            // Получение шагов алгоритма
            var algoSteps = await _context.AlgoSteps
                .Where(a => a.AlgoId == test.AlgoId)
                .ToListAsync();

            // Формирование ответа
            var testDetails = new TestDetailsDto
            {
                Test = test,
                Algorithm = algorithm,
                InputTestData = inputTestData,
                AlgoSteps = algoSteps
            };

            _logger.LogInformation($"Fetched test details: testId={testId}, algoId={test.AlgoId}, inputDataCount={inputTestData.Count}, algoStepsCount={algoSteps.Count}");
            _logger.LogDebug($"Test details response: {JsonSerializer.Serialize(testDetails)}");
            return Ok(testDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error fetching test details for testId={testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    public class TestDetailsDto
    {
        public Test Test { get; set; } = new Test();
        public Algorithm Algorithm { get; set; } = new Algorithm();
        public List<InputTestData> InputTestData { get; set; } = new List<InputTestData>();
        public List<AlgoStep> AlgoSteps { get; set; } = new List<AlgoStep>();
    }


    public class UpdateAlgoStepDto
    {
        public int AlgoId { get; set; }
        public int Step { get; set; }
        public float Difficult { get; set; }
    }

    public class UpdateTestDto
    {
        public int TestId { get; set; }
        public float difficult { get; set; }
        public int? SolvedCount { get; set; }
        public int? UnsolvedCount { get; set; }
    }


    [HttpDelete("delete-test/{testId}")]
    public async Task<IActionResult> DeleteTest(int testId)
    {
        try
        {
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            int algoId = test.AlgoId;
            string codeId = algoId.ToString();
            string testIdStr = testId.ToString();

            // Список файлов для удаления
            var filesToDelete = new List<string>
            {
                Path.Combine(StorageDirectory, $"test{codeId}_{testIdStr}.cs"),
                Path.Combine(StorageDirectory, $"test{codeId}_{testIdStr}errors.txt"),
                Path.Combine(StorageDirectory, $"test{codeId}_{testIdStr}modified.cs"),
                Path.Combine(StorageDirectory, $"test{codeId}_{testIdStr}output.txt"),
                Path.Combine(StorageDirectory, $"test{codeId}_{testIdStr}warnings.txt"),
                Path.Combine(StorageDirectory, $"{codeId}_{testIdStr}_errors.txt"),
                Path.Combine(StorageDirectory, $"{codeId}_{testIdStr}_output.txt"),
                Path.Combine(StorageDirectory, $"{codeId}_{testIdStr}_warnings.txt")
            };

            // Удаляем файлы, если они существуют
            foreach (var file in filesToDelete)
            {
                if (System.IO.File.Exists(file))
                {
                    System.IO.File.Delete(file);
                    _logger.LogInformation($"Deleted file: {file}");
                }
            }

            _context.Tests.Remove(test);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Deleted test: testId={testId}");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting test {testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // DTO для запросов
    public class CreateTestRequestDto
    {
        public Test Test { get; set; }
        public List<InputTestData> InputData { get; set; }
    }

    public class UpdateTestRequestDto
    {
        public Test Test { get; set; }
        public List<InputTestData> InputData { get; set; }
    }

    [HttpPut("create-helper/{testId}")]
    public async Task<IActionResult> CreateHelper(int testId, [FromBody] Test test)
    {
        try
        {
            var existingTest = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (existingTest == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            existingTest.TestName = test.TestName;
            existingTest.Description = test.Description;

            _context.Tests.Update(existingTest);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Updated test with name and description: testId={testId}, name={test.TestName}");
            return Ok(new { TestId = testId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating test {testId}: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }


    [HttpPut("update-test/{testId}")]
    public async Task<IActionResult> UpdateTest(int testId, [FromBody] UpdateTestRequestDto request)
    {
        var test = await _context.Tests.FindAsync(testId);
        if (test == null) return NotFound();

        test.AlgoId = request.Test.AlgoId;
        test.Description = request.Test.Description;
        test.TestName = request.Test.TestName;
        test.difficult = request.Test.difficult;
        test.SolvedCount = request.Test.SolvedCount;
        test.UnsolvedCount = request.Test.UnsolvedCount;

        var existingInputs = _context.InputData.Where(i => i.TestId == testId);
        _context.InputData.RemoveRange(existingInputs);

        foreach (var input in request.InputData)
        {
            input.TestId = testId;
            _context.InputData.Add(new InputTestData
            {
                TestId = input.TestId,
                VarName = input.VarName,
                VarValue = input.VarValue,
                VarType = input.VarType,
                LineNumber = input.LineNumber
            });
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

}

