using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace InterpretatorService.DTOs
{
    public class AlgorithmStepDetailDto
    {
        [JsonPropertyName("stepNumber")]
        public int StepNumber { get; set; } // 0 для входных данных, 1+ для отслеживания
        [JsonPropertyName("description")]

        public string Description { get; set; } = string.Empty; // Для шага 0: описание конкретной переменной. Для шагов >0: описание шага.
        [JsonPropertyName("lineNumber")]

        public int LineNumber { get; set; } // Для шага 0: строка инициализации переменной. Для шага >0: строка отслеживания.

        // Для шага 0 будет содержать ОДНО имя переменной.
        // Для шагов >0 будет содержать список переменных для отслеживания на данной LineNumber.
        [JsonPropertyName("variables")]
        public List<string> Variables { get; set; } = new List<string>();

        [JsonPropertyName("varType")]
        public string? VarType { get; set; } // Тип переменной (особенно актуально для шага 0)

        [JsonPropertyName("difficult")]
        public float? Difficult { get; set; }
    }
}
