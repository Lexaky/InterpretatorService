namespace InterpretatorService.DTOs
{
    public class UpdatePictureRequestDto
    {
        public int AlgoId { get; set; }
        public IFormFile ImageFile { get; set; }
    }
}
