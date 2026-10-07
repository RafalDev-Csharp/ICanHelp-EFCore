using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public class StringObject
    {
        public string Value { get; }
        public StringObject(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length is > 100 or < 1)
            {
                throw new InvalidStringObjectException(value);
            }

            Value = value;
        }

        public static implicit operator StringObject(string value) => value is null ? null : new(value);
        public static implicit operator string(StringObject customName) => customName?.Value;
        public override string ToString() => Value;
    }
}
