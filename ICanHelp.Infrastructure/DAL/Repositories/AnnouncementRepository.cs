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
    internal class AnnouncementRepository : Repository<Announcement>, IAnnouncementRepository
    {
        public Task AddAnnouncementAsync(Announcement announcement)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAnnouncementAsync(Announcement announcement)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Announcement>> GetAllAnnouncementsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Announcement?> GetAnnouncementByIdAsync(Id announcementId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAnnouncementAsync(Announcement announcement)
        {
            throw new NotImplementedException();
        }
    }
}
