namespace InterpretatorService.DTOs
{
    public class AlgorithmEditorBundleDto
    {
        public int AlgoId { get; set; }
        public string AlgorithmName { get; set; } = string.Empty;
        public string? PicPath { get; set; }
        public string CodeContent { get; set; } = string.Empty;
        // Содержит все шаги: шаг 0 (каждая переменная как отдельный элемент) и шаги > 0
        public List<AlgorithmStepDetailDto> AllConfiguredSteps { get; set; } = new List<AlgorithmStepDetailDto>();
    }
}
