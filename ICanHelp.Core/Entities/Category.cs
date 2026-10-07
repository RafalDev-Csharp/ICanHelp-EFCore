using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class Category
    {
        public Id Id { get; set; }

        public StringObject CategoryName { get; set; }

        public IEnumerable<Announcement> Announcements { get; set; }
        public IEnumerable<Subcategory> Subcategories { get; set; }

        public Category(Id id, StringObject categoryName)
        {
            Id = id;
            CategoryName = categoryName;
        }

        public void UpdateCategory(StringObject categoryName)
        {
            CategoryName = categoryName;
        }
    }
}
