using InterpretatorService.Models;

namespace InterpretatorService.Interfaces
{
    public interface IInterpreterService
    {
        Task<CodeModel> ExecuteCodeAsync(string codeFilePath);
    }
}