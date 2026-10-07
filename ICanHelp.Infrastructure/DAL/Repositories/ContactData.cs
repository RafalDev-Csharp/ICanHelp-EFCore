using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Repositories
{
    internal class ContactData : IContactData
    {
        public Task AddContactDataAsync(string contactData)
        {
            throw new NotImplementedException();
        }

        public Task AddContactDataAsync(Core.Entities.Users.ContactData contactData)
        {
            throw new NotImplementedException();
        }

        public Task DeleteContactDataAsync(string contactData)
        {
            throw new NotImplementedException();
        }

        public Task DeleteContactDataAsync(Core.Entities.Users.ContactData contactData)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Core.Entities.Users.ContactData>> GetAllContactDataAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Core.Entities.Users.ContactData?> GetContactDataByIdAsync(Id contactData)
        {
            throw new NotImplementedException();
        }

        public Task UpdateContactDataAsync(Core.Entities.Users.ContactData contactData)
        {
            throw new NotImplementedException();
        }
    }
}
