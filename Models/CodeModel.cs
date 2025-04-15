namespace InterpretatorService.Models
{
    public class CodeModel
    {
        public int CodeId { get; }
        public string Path { get; }
        public string StandardOutput { get; set; }
        public string ErrorOutput { get; set; }
        public string WarningOutput { get; set; }
        public string OutputFilePath { get; set; }
        public string ErrorFilePath { get; set; }
        public string WarningFilePath { get; set; }
        public bool IsSuccessful { get; set; }

        public CodeModel(int codeId, string codeFilePath)
        {
            CodeId = codeId;
            Path = codeFilePath;
            // Формируем пути для файлов ошибок, предупреждений и вывода
            string basePath = System.IO.Path.GetDirectoryName(codeFilePath);
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(codeFilePath);
            ErrorFilePath = System.IO.Path.Combine(basePath, $"{fileNameWithoutExtension}errors.txt");
            WarningFilePath = System.IO.Path.Combine(basePath, $"{fileNameWithoutExtension}warnings.txt");
            OutputFilePath = System.IO.Path.Combine(basePath, $"{fileNameWithoutExtension}output.txt");

            Console.WriteLine($"CodeModel created: CodeId={codeId}, Path={codeFilePath}, ErrorFilePath={ErrorFilePath}");
        }
    }
}