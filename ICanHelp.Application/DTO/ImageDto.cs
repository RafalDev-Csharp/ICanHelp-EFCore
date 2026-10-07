using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class ImageDto
    {
        public Guid Id { get; set; }

        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public string FilePath { get; set; }

        public AnnouncementDto Announcement { get; set; }
        public Guid AnnouncementId { get; set; }
    }
}
