namespace InterpretatorService.Services
{
    public class VariableUpdateDto
    {
        public int LineNumber { get; set; }
        public string VariableName { get; set; }
        public string Value { get; set; }
    }
}
