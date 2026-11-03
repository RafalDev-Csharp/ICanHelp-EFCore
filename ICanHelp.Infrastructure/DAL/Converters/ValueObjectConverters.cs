using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ICanHelp.Infrastructure.DAL.Converters;

// Value objects are stored as plain columns (Guid, string, int).
// The converters are registered once for the whole model in ICanHelpDbContext.ConfigureConventions.

internal sealed class IdConverter() : ValueConverter<Id, Guid>(x => x.Value, x => new Id(x));

internal sealed class UserIdConverter() : ValueConverter<UserId, Guid>(x => x.Value, x => new UserId(x));

internal sealed class StringObjectConverter() : ValueConverter<StringObject, string>(x => x.Value, x => new StringObject(x));

internal sealed class DescriptionConverter() : ValueConverter<Description, string>(x => x.Value, x => new Description(x));

internal sealed class UserNameConverter() : ValueConverter<UserName, string>(x => x.Value, x => new UserName(x));

internal sealed class FullNameConverter() : ValueConverter<FullName, string>(x => x.Value, x => new FullName(x));

internal sealed class EmailConverter() : ValueConverter<Email, string>(x => x.Value, x => new Email(x));

internal sealed class PhoneNumberConverter() : ValueConverter<PhoneNumber, string>(x => x.Value, x => new PhoneNumber(x));

internal sealed class RateConverter() : ValueConverter<Rate, int>(x => x.Value, x => new Rate(x));
