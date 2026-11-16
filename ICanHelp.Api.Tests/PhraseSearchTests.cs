using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>
/// F11 B: wyszukiwanie tekstowe (<c>phrase</c>) w tytule i opisie na prawdziwym SQL Serverze (LIKE, bez rozróżniania
/// wielkości liter). Każdy test filtruje po swoim właścicielu – baza jest wspólna.
/// </summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class PhraseSearchTests
{
    private readonly ApiFactory _factory;

    public PhraseSearchTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Phrase_MatchesTitleOrDescription_IgnoringCase()
    {
        var owner = await _factory.CreateUserAsync();
        var fence = await owner.CreateAnnouncementAsync(title: "Malowanie płotu");
        var grass = await owner.CreateAnnouncementAsync(title: "Koszenie trawy");

        var byTitle = await Search(owner, "PŁOT");
        byTitle.Select(a => a.Id).ShouldBe(new[] { fence });

        // Opis z helpera testowego: „Kran cieknie od tygodnia, mam nową uszczelkę.”
        var byDescription = await Search(owner, " uszczelk ");
        byDescription.Select(a => a.Id).ShouldBe(new[] { grass, fence }, ignoreOrder: true);
    }

    [Fact]
    public async Task Phrase_WildcardsAreLiteral()
    {
        var owner = await _factory.CreateUserAsync();
        await owner.CreateAnnouncementAsync(title: "Malowanie płotu");

        (await Search(owner, "%")).ShouldBeEmpty();
        (await Search(owner, "Malowanie_płotu")).ShouldBeEmpty();
    }

    [Fact]
    public async Task TooLongPhrase_Returns400()
    {
        var response = await _factory.CreateClient()
            .GetAsync($"/api/announcements?phrase={new string('a', 101)}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidSearchPhrase");
    }

    private static async Task<List<AnnouncementDto>> Search(TestUser owner, string phrase)
    {
        var result = await owner.Client.GetFromJsonAsync<List<AnnouncementDto>>(
            $"/api/announcements?ownerId={owner.Id}&phrase={Uri.EscapeDataString(phrase)}");
        result.ShouldNotBeNull();
        return result;
    }
}
