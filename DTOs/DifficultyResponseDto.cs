namespace InterpretatorService.DTOs
{
    public class DifficultyResponseDto
    {
        public int AlgoId { get; set; }
        public Dictionary<int, float> StepDifficulties { get; set; } = new();
        public Dictionary<int, float> TestDifficulties { get; set; } = new();
        public float AlgorithmDifficulty { get; set; }
    }
}
