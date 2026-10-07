using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record FullName
    {
        public string Value { get; }
        public FullName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length is > 100 or < 3)
            {
                throw new InvalidFullNameException(value);
            }

            Value = value;
        }

        public static implicit operator FullName(string value) => value is null ? null : new(value);
        public static implicit operator string(FullName fullName) => fullName?.Value;
        public override string ToString() => Value;
    }
}
