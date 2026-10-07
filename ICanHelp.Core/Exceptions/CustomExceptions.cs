using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public abstract class CustomExceptions : Exception
    {
        public CustomExceptions(string message) : base(message)
        {
        }
    }
}
