using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record Description
    {
        public string Value { get; }
        public Description(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length is > 500 or < 2)
            {
                throw new InvalidDescriptionException(value);
            }

            Value = value;
        }

        public static implicit operator Description(string value) => new(value);
        public static implicit operator string(Description username) => username.Value;
        public override string ToString() => Value;
    }
}
