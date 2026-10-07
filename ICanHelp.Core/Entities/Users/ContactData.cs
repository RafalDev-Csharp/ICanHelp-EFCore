using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Core.Entities.Users
{
    public class ContactData
    {
        public Id Id { get; private set; }
        public Email Email { get; set; }

        public User User { get; set; }
        public UserId UserId { get; set; }

        public PhoneNumber PhoneNumber { get; set; }
        public StringObject Country { get; set; }
        public StringObject City { get; set; }
        public StringObject PostalCode { get; set; }

        public ContactData(Id id, Email email, PhoneNumber phoneNumber, User user, UserId userId, StringObject country, StringObject city, StringObject postalCode)
        {
            Id = id;
            Email = email;
            PhoneNumber = phoneNumber;
            User = user;
            UserId = userId;
            Country = country;
            City = city;
            PostalCode = postalCode;
        }

        public void UpdateContactData(Email email, PhoneNumber phoneNumber, StringObject country, StringObject city, StringObject postalCode)
        {
            Email = email;
            PhoneNumber = phoneNumber;
            Country = country;
            City = city;
            PostalCode = postalCode;
        }
    }
}
