using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL
{
    public interface IUnitOfWork
    {
        Task ExecuteAsync(Func<Task> action);
    }
}
