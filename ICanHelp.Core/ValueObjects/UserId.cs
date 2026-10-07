using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public sealed record UserId
    {
        public Guid Value { get; }

        public UserId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new InvalidIdentityException(value);
            }

            Value = value;
        }

        public static implicit operator Guid(UserId value) => value.Value;
        public static implicit operator UserId(Guid value) => new(value);
    }
}
