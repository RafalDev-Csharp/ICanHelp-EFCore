using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public class InvalidStringObjectException : CustomExceptions
    {
        public string StringObject { get; }
        public InvalidStringObjectException(string strObject) : base($"StringObject: {strObject} is incorrect.")
        {
            StringObject = strObject;
        }
    }
}
