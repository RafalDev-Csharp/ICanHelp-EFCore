using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.Queries
{
    public class GetImageById : IQuery<ImageDto>
    {
        public Guid ImageId { get; set; }
    }
}
