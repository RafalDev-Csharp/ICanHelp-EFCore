using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Repositories
{
    internal class LikedUserRepository : ILikedUserRepository
    {
        public Task AddLikedUserAsync(LikedUser likedUser)
        {
            throw new NotImplementedException();
        }

        public Task DeleteLikedUserAsync(LikedUser likedUser)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<LikedUser>> GetAllLikedUsersAsync()
        {
            throw new NotImplementedException();
        }

        public Task<LikedUser?> GetLikedUserByIdAsync(Id likedUserId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLikedUserAsync(LikedUser likedUser)
        {
            throw new NotImplementedException();
        }
    }
}
