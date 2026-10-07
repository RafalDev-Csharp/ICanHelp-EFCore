using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record UserName
    {
        public string Value { get; }
        public UserName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length is > 30 or < 3)
            {
                throw new InvalidStringObjectException(value);
            }

            Value = value;
        }

        public static implicit operator UserName(string value) => new(value);
        public static implicit operator string(UserName username) => username?.Value;
        public override string ToString() => Value;
    }
}
