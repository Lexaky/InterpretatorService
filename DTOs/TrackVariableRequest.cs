namespace InterpretatorService.DTOs
{
    public class TrackVariableRequest
    {
        public int LineNumber { get; set; }
        public string VarType { get; set; }
        public string VarName { get; set; }
        public int Step { get; set; }
    }
}
