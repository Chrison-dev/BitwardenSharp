namespace Bitwarden.Application
{
    public interface IConsole
    {
        void WriteLine(string? value);
        void WriteError(string? value);
        string ReadLineMasked();
    }
}
