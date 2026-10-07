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
    internal class ImagesRepository : IImagesRepository
    {
        public Task AddImageAsync(Image image)
        {
            throw new NotImplementedException();
        }

        public Task DeleteImageAsync(Image image)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Image>> GetAllImagesAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Image?> GetImageByIdAsync(Id imageId)
        {
            throw new NotImplementedException();
        }

        public Task UpdateImageAsync(Image image)
        {
            throw new NotImplementedException();
        }
    }
}
