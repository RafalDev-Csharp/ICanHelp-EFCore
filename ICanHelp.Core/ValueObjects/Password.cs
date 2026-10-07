using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record Password
    {
        public string Value { get; }
        public Password(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length is > 200 or < 6)
            {
                throw new InvalidPasswordException();
            }

            Value = value;
        }

        public static implicit operator Password(string value) => new(value);
        public static implicit operator string(Password password) => password.Value;
        public override string ToString() => Value;
    }
}
