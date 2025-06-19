namespace InterpretatorService.DTOs
{
    public class UpdateStepResponseDto
    {
        public int TestId { get; set; }
        public int AlgoId { get; set; }
        public List<StepResultDto> StepResults { get; set; } = new();
    }
}
