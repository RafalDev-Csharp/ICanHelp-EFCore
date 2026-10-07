using ICanHelp.Core.Entities.Liked;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Repositories
{
    public interface IContactData : IRepository<ContactData>
    {
        void Update(ContactData contactData);
    }
}
