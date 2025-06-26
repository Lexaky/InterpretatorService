using System.Text.Json.Serialization;

namespace InterpretatorService.Models
{
    public class InputTestData
    {
        [JsonPropertyName("testId")]
        public int TestId { get; set; }
        [JsonPropertyName("varName")]
        public string VarName { get; set; }
        [JsonPropertyName("varValue")]
        public string VarValue { get; set; }
        [JsonPropertyName("varType")]
        public string VarType {  get; set; }
        [JsonPropertyName("lineNumber")]
        public int LineNumber { get; set; }
    }
}
