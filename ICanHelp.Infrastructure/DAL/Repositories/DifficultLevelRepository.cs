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
    internal class DifficultLevelRepository : IDifficultLevelRepository
    {
        public Task AddDifficultLevelAsync(DifficultLevel difficultLevel)
        {
            throw new NotImplementedException();
        }

        public Task DeleteDifficultLevelAsync(DifficultLevel difficultLevel)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<DifficultLevel>> GetAllDifficultLevelsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<DifficultLevel?> GetDifficultLevelByIdAsync(Id difficultLevelId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateDifficultLevelAsync(DifficultLevel difficultLevel)
        {
            throw new NotImplementedException();
        }
    }
}
