using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class CategoryDto
    {
        public Guid Id { get; set; }

        public string CategoryName { get; set; }

        public IEnumerable<AnnouncementDto> Announcements { get; set; }
        public IEnumerable<SubcategoryDto> Subcategories { get; set; }
    }
}
