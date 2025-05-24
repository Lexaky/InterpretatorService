namespace InterpretatorService.DTOs
{
    public class CodeExecutionResponse
    {
        public bool IsSuccessful { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public List<TestVariableTracker.ValueData> Values { get; set; }
        public List<TestVariableTracker.MismatchData> Mismatches { get; set; }
        public long ExecutionTime { get; set; }
    }
}
