using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InterpretatorService.Data;
using InterpretatorService.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InterpretatorService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestManagementController : ControllerBase
{
    private readonly TestsDbContext _context;
    private readonly ILogger<TestManagementController> _logger;

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
    public async Task<IActionResult> ModifyTest(int testId, [FromBody] dynamic update)
    {
        try
        {
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            if (update.difficult != null)
            {
                test.difficult = (float)update.difficult;
            }
            if (update.SolvedCount != null)
            {
                test.SolvedCount = (int)update.SolvedCount;
            }
            if (update.UnsolvedCount != null)
            {
                test.UnsolvedCount = (int)update.UnsolvedCount;
            }

            _context.Tests.Update(test);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated test: testId={testId}");
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
    public async Task<IActionResult> ModifyAlgoStep(int algoId, int step, [FromBody] dynamic update)
    {
        try
        {
            var algoStep = await _context.AlgoSteps
                .FirstOrDefaultAsync(a => a.AlgoId == algoId && a.Step == step);
            if (algoStep == null)
            {
                _logger.LogWarning($"Algo step not found: algoId={algoId}, step={step}");
                return NotFound($"Algo step not found: algoId={algoId}, step={step}");
            }

            if (update.Difficult != null)
            {
                algoStep.Difficult = (float)update.Difficult;
            }

            _context.AlgoSteps.Update(algoStep);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated algo step: algoId={algoId}, step={step}");
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
            var test = await _context.Tests.FirstOrDefaultAsync(t => t.TestId == testId);
            if (test == null)
            {
                _logger.LogWarning($"Test not found: testId={testId}");
                return NotFound($"Test not found: testId={testId}");
            }

            foreach (var stepResponse in stepResponses)
            {
                var existing = await _context.TestStepResponses
                    .FirstOrDefaultAsync(tsr => tsr.TestId == testId && tsr.AlgoStep == stepResponse.AlgoStep && tsr.AlgoId == test.AlgoId);
                if (existing == null)
                {
                    var newStepResponse = new TestStepResponse
                    {
                        TestId = stepResponse.TestId,
                        AlgoStep = stepResponse.AlgoStep,
                        AlgoId = test.AlgoId,
                        CorrectCount = stepResponse.CorrectCount,
                        IncorrectCount = stepResponse.IncorrectCount
                    };
                    _context.TestStepResponses.Add(newStepResponse);
                }
                else
                {
                    existing.CorrectCount = stepResponse.CorrectCount;
                    existing.IncorrectCount = stepResponse.IncorrectCount;
                    _context.TestStepResponses.Update(existing);
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
}