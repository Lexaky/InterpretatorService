namespace InterpretatorService.DTOs
{
    public class TestStepResponseDto
    {
        public int TestId { get; set; }
        public int AlgoStep { get; set; }
        public int CorrectCount { get; set; }
        public int IncorrectCount { get; set; }
    }
}
