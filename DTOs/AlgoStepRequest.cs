namespace InterpretatorService.DTOs
{
    public class AlgoStepRequest
    {
        public int AlgoId { get; set; }
        public int Step { get; set; }
        public string Description { get; set; }
        public float? Difficult { get; set; }
    }
}
