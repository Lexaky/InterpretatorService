namespace InterpretatorService.DTOs
{
    public class CreateAlgorithmRequestDto
    {
        public string AlgorithmName { get; set; }
        public string CodeContent { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string AllStepsJson { get; set; }
    }
}
