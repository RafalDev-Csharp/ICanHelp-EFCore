using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Repositories
{
    internal class RatingRepository : IRatingRepository
    {
        public Task AddRatingAsync(int rating)
        {
            throw new NotImplementedException();
        }

        public Task DeleteRatingAsync(int rating)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<int>> GetAllRatingsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<int?> GetRatingByIdAsync(Id ratingId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateRatingAsync(int rating)
        {
            throw new NotImplementedException();
        }
    }
}
