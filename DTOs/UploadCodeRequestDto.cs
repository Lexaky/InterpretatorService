namespace InterpretatorService.DTOs
{
    public class UploadCodeRequestDto
    {
        public IFormFile CodeFile { get; set; }
        public IFormFile MetaFile { get; set; }
        public int AlgorithmId { get; set; }
    }
}