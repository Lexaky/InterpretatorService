namespace InterpretatorService.DTOs
{
    public class CodeRequestDto
    {
        /// <summary>
        /// Язык программирования (например, C#, Python, JS и т.д.)
        /// </summary>
        public string Language { get; set; } = "cs"; // C# by default

        /// <summary>
        /// Код, который нужно интерпретировать
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Тайм-аут выполнения в секундах (опционально)
        /// </summary>
        public int Timeout { get; set; } = 20;
    }
}
