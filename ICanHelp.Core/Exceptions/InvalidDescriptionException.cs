using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Exceptions
{
    public class InvalidDescriptionException : CustomExceptions
    {
        public string Description { get; }
        public InvalidDescriptionException(string description) : base($"Description: {description} is not valid.")
        {
            Description = description;
        }
    }
}
