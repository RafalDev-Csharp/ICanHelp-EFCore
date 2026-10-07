using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class Image
    {
        public Id Id { get; set; }

        public StringObject FileName{ get; set; }
        public StringObject FileExtension{ get; set; }
        public StringObject FilePath{ get; set; }

        public Announcement Announcement { get; set; }
        public Id AnnouncementId { get; set; }

        public Image(Id id, StringObject fileName, StringObject fileExtension, StringObject filePath)
        {
            id = Id;
            FileName = fileName;
            FileExtension = fileExtension;
            FilePath = filePath;
        }

        public void UpdateImage(StringObject fileName, StringObject fileExtension, StringObject filePath, Id announcementId)
        {
            FileName = fileName;
            FileExtension = fileExtension;
            FilePath = filePath;
            AnnouncementId = announcementId;
        }
    }
}
