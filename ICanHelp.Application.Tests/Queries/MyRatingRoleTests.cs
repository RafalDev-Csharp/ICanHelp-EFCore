using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Tests.Queries;

/// <summary>F11 F: rola w ocenianej transakcji – segment „jako pomocnik / jako zleceniodawca” na ekranie „Oceny”.</summary>
public class MyRatingRoleTests
{
    [Fact]
    public void Owner_of_the_announcement_is_the_client()
    {
        var ownerId = Guid.NewGuid();

        MyRatingDto.RoleOf(ownerId, ownerId).ShouldBe("Client");
    }

    [Fact]
    public void Anyone_else_rated_in_the_announcement_is_the_helper()
    {
        MyRatingDto.RoleOf(Guid.NewGuid(), Guid.NewGuid()).ShouldBe("Helper");
    }
}
