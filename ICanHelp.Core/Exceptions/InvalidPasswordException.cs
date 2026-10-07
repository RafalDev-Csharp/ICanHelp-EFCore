using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public class InvalidPasswordException : CustomExceptions
    {
        public InvalidPasswordException() : base("Invalid password.")
        {
        }
    }
}
