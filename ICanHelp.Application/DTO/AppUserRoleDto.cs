using ICanHelp.Core.Entities.Negotiations;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Application.DTO
{
    public class AppUserRoleDto
    {
        public Guid Id { get; set; }

        public string UserRoleName { get; set; }

        public Guid UserId { get; set; }
        public UserDto User { get; set; }

        public IEnumerable<ClientOfferDto> ClientOffers { get; set; }
        public IEnumerable<HelperOfferDto> HelperOffers { get; set; }
    }
}
