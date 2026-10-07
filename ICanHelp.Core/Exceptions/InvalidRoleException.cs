using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    internal class InvalidRoleException : CustomExceptions
    {
        public string Role { get; set; }
        public InvalidRoleException(string role) : base($"Role: {role} is invalid.")
        {
            Role = role;
        }
    }
}
