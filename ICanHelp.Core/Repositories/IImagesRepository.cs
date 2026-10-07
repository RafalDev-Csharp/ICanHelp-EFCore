using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Repositories
{
    public interface IImagesRepository : IRepository<Image>
    {
        void Update(Image image);
    }
}
