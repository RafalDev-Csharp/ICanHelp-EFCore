using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public record PhoneNumber
    {
        private readonly Regex Regex = new(@"^\+?[1-9][0-9]{7,14}$");
        public string Value { get; }

        public PhoneNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidPhoneNumberException(value);
            }

            if (value.Length is > 25 or < 6)
            {
                throw new InvalidPhoneNumberException(value);
            }

            if (!Regex.IsMatch(value))
            {
                throw new InvalidPhoneNumberException(value);
            }

            Value = value;
        }

        public static implicit operator string(PhoneNumber phoneNumber) => phoneNumber.Value;
        public static implicit operator PhoneNumber(string phoneNumber) => new(phoneNumber);
        public override string ToString() => Value;
    }
}
