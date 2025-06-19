namespace InterpretatorService.DTOs
{
    public class UpdateTestDto
    {
        public int TestId { get; set; }
        public int SolvedCount { get; set; }
        public int UnsolvedCount { get; set; }
    }
}
