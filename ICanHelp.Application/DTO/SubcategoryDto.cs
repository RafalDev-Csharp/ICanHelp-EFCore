using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class SubcategoryDto
    {
        public Guid Id { get; set; }

        public string SubcategoryName { get; set; }

        public Guid CategoryId { get; set; }
        public CategoryDto Category { get; set; }
    }
}
