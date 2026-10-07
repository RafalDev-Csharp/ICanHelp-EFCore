using ICanHelp.Core.Entities;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Repositories
{
    internal class SubcategoryRepository : ISubcategoryRepository
    {
        public Task AddSubcategoryAsync(Subcategory subcategory)
        {
            throw new NotImplementedException();
        }

        public Task DeleteSubcategoryAsync(Subcategory subcategory)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Subcategory>> GetAllSubcategoriesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Subcategory?> GetSubcategoryByIdAsync(Id subcategoryId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateSubcategoryAsync(Subcategory subcategory)
        {
            throw new NotImplementedException();
        }
    }
}
