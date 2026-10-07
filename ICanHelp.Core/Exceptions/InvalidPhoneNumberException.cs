using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    internal class InvalidPhoneNumberException : CustomExceptions
    {
        public string PhoneNumber { get; }
        public InvalidPhoneNumberException(string phoneNumber) : base($"Phone number: {phoneNumber} is invalid.")
        {
            PhoneNumber = phoneNumber;
        }
    }
}
