using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    internal class InvalidEmailException : CustomExceptions
    {
        public string Email { get; }
        public InvalidEmailException(string email) : base($"Email: {email} is invalid.")
        {
            Email = email;
        }
    }
}
