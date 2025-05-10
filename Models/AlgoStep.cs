namespace InterpretatorService.Models
{
    public class AlgoStep
    {
        public int AlgoId { get; set; }
        public int Sequence { get; set; }
        public int Step { get; set; }
        public string Type { get; set; }
        public string VarName { get; set; }
        public string Value { get; set; }
    }
}
