using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities
{
    public class Subcategory
    {
        public Id Id { get; set; }

        public StringObject SubcategoryName { get; set; }

        public Id CategoryId { get; set; }
        public Category Category { get; set; }


        public Subcategory(Id id, StringObject subcategoryName, Id categoryId)
        {
            Id = id;
            SubcategoryName = subcategoryName;
            CategoryId = categoryId;
        }

        public void UpdateSubcategory(StringObject subcategoryName, Id categoryId)
        {
            SubcategoryName = subcategoryName;
            CategoryId = categoryId;
        }
    }
}
