using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public class InvalidIdentityException : CustomExceptions
    {
        public object Id { get; }
        public InvalidIdentityException(object id) : base($"Cannot set: {id} as entity identifier.")
        {
            Id = id;
        }
    }
}
