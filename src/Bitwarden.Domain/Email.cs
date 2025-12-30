using System;
using System.Text.RegularExpressions;

namespace Bitwarden.Domain
{
    public sealed class Email
    {
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public string Value { get; }

        private Email(string value)
        {
            Value = value;
        }

        public static Email Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Email cannot be empty", nameof(value));
            if (!EmailRegex.IsMatch(value)) throw new ArgumentException("Invalid email format", nameof(value));
            return new Email(value);
        }

        public static bool TryParse(string value, out Email? email)
        {
            email = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (!EmailRegex.IsMatch(value)) return false;
            email = new Email(value);
            return true;
        }

        public override string ToString() => Value;

        public static implicit operator string(Email e) => e.Value;
        public static explicit operator Email(string s) => Parse(s);
    }
}
