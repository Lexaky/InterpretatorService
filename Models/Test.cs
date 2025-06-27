using System.Text.Json.Serialization;

namespace InterpretatorService.Models
{
    public class Test
    {
        [JsonPropertyName("algoId")]
        public int AlgoId { get; set; }
        [JsonPropertyName("testId")]
        public int TestId { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }
        [JsonPropertyName("testName")]
        public string TestName { get; set; }
        [JsonPropertyName("difficult")]
        public float difficult { get; set; } = 0.5f;
        [JsonPropertyName("solvedCount")]
        public int SolvedCount { get; set; } = 0;
        [JsonPropertyName("unsolvedCount")]
        public int UnsolvedCount { get; set; } = 0;
    }
}
