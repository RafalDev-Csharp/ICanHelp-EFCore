using ICanHelp.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.ValueObjects
{
    public record Id
    {
        public Guid Value { get; }

        public Id(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new InvalidIdentityException(value);
            }

            Value = value;
        }

        public static implicit operator Guid(Id value) => value.Value;
        public static implicit operator Id(Guid value) => new(value);
    }
}
