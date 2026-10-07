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
    internal class AppUserRoleRepository : IAppUserRoleRepository
    {
        public Task AddAppUserRoleAsync(AppUserRole appUserRole)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAppUserRoleAsync(AppUserRole appUserRole)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<AppUserRole>> GetAllAppUserRolesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<AppUserRole?> GetAppUserRoleByIdAsync(Id appUserRoleId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAppUserRoleAsync(AppUserRole appUserRole)
        {
            throw new NotImplementedException();
        }
    }
}
