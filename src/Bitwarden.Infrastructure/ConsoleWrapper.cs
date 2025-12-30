using Bitwarden.Application;
using System.Text;

namespace Bitwarden.Infrastructure
{
    public class ConsoleWrapper : IConsole
    {
        public void WriteLine(string? value) => Console.WriteLine(value);

        public void WriteError(string? value) => Console.Error.WriteLine(value);

        public string ReadLineMasked()
        {
            var sb = new StringBuilder();
            ConsoleKeyInfo key;
            while (true)
            {
                key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    sb.Append(key.KeyChar);
                    Console.Write('*');
                }
            }
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
