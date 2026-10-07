using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class LocationDto
    {
        public Guid Id { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public AnnouncementDto Announcement { get; set; }
        public Guid AnnouncementId { get; set; }
    }
}
