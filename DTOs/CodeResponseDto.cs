namespace InterpretatorService.DTOs
{
    public class CodeResponseDto
    {
        /// <summary>
        /// Результат выполнения кода
        /// </summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Ошибки, возникшие при интерпретации
        /// </summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Время выполнения кода (в миллисекундах)
        /// </summary>
        public long ExecutionTime { get; set; }

        /// <summary>
        /// Успешно ли завершилось выполнение
        /// </summary>
        public bool IsSuccessful { get; set; }
    }
}
