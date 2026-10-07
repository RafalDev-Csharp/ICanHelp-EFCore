using ICanHelp.Application.Abstractions;
using ICanHelp.Application.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.Queries
{
    public class GetAnnouncements : IQuery<IEnumerable<AnnouncementDto>>
    {
        public Guid UserId { get; set; }
    }
}
