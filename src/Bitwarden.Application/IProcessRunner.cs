namespace Bitwarden.Application
{
    public interface IProcessRunner
    {
        bool IsAvailable(string executable, string args);
        Task<int> RunAsync(string executable, string arguments, TextWriter? output = null, TextWriter? error = null);
    }
}
