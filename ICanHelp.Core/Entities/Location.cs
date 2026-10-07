using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class Location
    {
        public Id Id { get; set; }

        public double? Latitude { get; set; }         
        public double? Longitude { get; set; }

        public Id AnnouncementId { get; set; }
        public Announcement Announcement { get; set; }



        public Location(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        public void UpdateLocation(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
    }
}
