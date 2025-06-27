using InterpretatorService.Models;

namespace InterpretatorService.DTOs
{
    public class CreateTestRequestDto
    {
        public Test Test { get; set; }
        public List<InputTestData> InputData { get; set; }
    }
}
