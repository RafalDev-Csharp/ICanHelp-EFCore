using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class DifficultLevel
    {
        public Id Id { get; set; }

        public StringObject DifficultLevelName { get; set; }
        public IEnumerable<Announcement> Announcements { get; set; }

        public DifficultLevel(Id id, StringObject difficultLevelName, IEnumerable<Announcement> announcements)
        {
            Id = id;
            DifficultLevelName = difficultLevelName;
            Announcements = announcements;
        }

        public void UpdateDifficultLevel(StringObject difficultLevelName)
        {
            DifficultLevelName = difficultLevelName;
        }
    }
}
