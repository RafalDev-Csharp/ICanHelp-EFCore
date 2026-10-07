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
    internal class LikedAnnouncementRepository : ILikedAnnouncementRepository
    {
        public Task AddLikedAnnouncementAsync(LikedAnnouncement likedAnnouncement)
        {
            throw new NotImplementedException();
        }

        public Task DeleteLikedAnnouncementAsync(LikedAnnouncement likedAnnouncement)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<LikedAnnouncement>> GetAllLikedAnnouncementsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<LikedAnnouncement?> GetLikedAnnouncementByIdAsync(Id likedAnnouncementId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLikedAnnouncementAsync(LikedAnnouncement likedAnnouncement)
        {
            throw new NotImplementedException();
        }
    }
}
