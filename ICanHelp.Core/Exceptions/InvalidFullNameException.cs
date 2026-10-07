using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public class InvalidFullNameException : CustomExceptions
    {
        public string FullName { get; set; }
        public InvalidFullNameException(string fullName) : base($"Full name: {fullName} is incorrect.")
        {
            FullName = fullName;
        }
    }
}
