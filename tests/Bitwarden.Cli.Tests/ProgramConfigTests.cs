using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.IO;
using Bitwarden.Application;
using Bitwarden.Infrastructure;

namespace Bitwarden.Cli.Tests
{
    public class ProgramConfigTests
    {
        [SetUp]
        public void Setup()
        {
            // Tell host to use in-memory secret store
            Environment.SetEnvironmentVariable("BITWARDEN_INMEMORY_SECRETS", "1");

            var app = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "bitwarden-cli-helper");
            if (Directory.Exists(app)) Directory.Delete(app, true);
        }

        [Test]
        public async Task ConfigSetGetClear_Works()
        {
            // set email
            var setEmailArgs = new string[] { "config", "set", "email", "user@example.com" };
            await Program.Main(setEmailArgs);

            var getEmailArgs = new string[] { "config", "get", "email" };
            await Program.Main(getEmailArgs);

            // set password via interactive style (use direct value)
            var setPasswordArgs = new string[] { "config", "set", "password", "P@ssw0rd!" };
            await Program.Main(setPasswordArgs);

            var getPasswordArgs = new string[] { "config", "get", "password" };
            await Program.Main(getPasswordArgs);

            // clear
            var clearArgs = new string[] { "config", "clear" };
            await Program.Main(clearArgs);
        }
    }
}
