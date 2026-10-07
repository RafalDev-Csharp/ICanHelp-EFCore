using ICanHelp.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Repositories
{
    public interface ILocationRepository : IRepository<Location>
    {
        void Update(Location location);
    }
}
