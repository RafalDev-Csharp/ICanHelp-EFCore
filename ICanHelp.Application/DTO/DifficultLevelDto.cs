using ICanHelp.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class DifficultLevelDto
    {
        public Guid Id { get; set; }

        public string DifficultLevelName { get; set; }

        public IEnumerable<AnnouncementDto> Announcements { get; set; }
    }
}
