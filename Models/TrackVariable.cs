namespace InterpretatorService.Models
{
    public class TrackVariable
    {
        public int LineNumber { get; set; }
        public string VarType { get; set; }
        public string VarName { get; set; }
        public int Step { get; set; }
        public int Sequence { get; set; }

    }
}
