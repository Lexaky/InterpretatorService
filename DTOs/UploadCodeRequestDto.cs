namespace InterpretatorService.DTOs
{
    public class UploadCodeRequestDto
    {
        public IFormFile CodeFile { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string AlgorithmName { get; set; }
    }
}