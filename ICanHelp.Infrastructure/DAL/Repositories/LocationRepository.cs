using ICanHelp.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Repositories
{
    internal class LocationRepository : ILocationRepository
    {
        public Task AddLocationAsync(string location)
        {
            throw new NotImplementedException();
        }

        public Task DeleteLocationAsync(string location)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetAllLocationsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string?> GetLocationByIdAsync(Guid locationId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateLocationAsync(string location)
        {
            throw new NotImplementedException();
        }
    }
}
