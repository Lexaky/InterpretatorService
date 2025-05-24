namespace InterpretatorService.Models
{
    public class CodeExecutionResult
    {
        public bool IsSuccessful { get; set; }
        public string StandardOutput { get; set; }
        public string ErrorOutput { get; set; }
    }
}
