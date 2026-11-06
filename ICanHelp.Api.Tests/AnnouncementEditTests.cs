using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok 9: PATCH ogłoszenia, rewizje i ostrzeżenie „ogłoszenie zmieniło się po tej ofercie”.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AnnouncementEditTests
{
    private readonly ApiFactory _factory;

    public AnnouncementEditTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Edit_MakesPendingOfferOutdated_CounterOfferCanBeAccepted()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);

        // Właściciel dopisuje coś do opisu – wysyła tylko to pole.
        var patch = await owner.Client.PatchAsJsonAsync(url,
            new { description = "Kran cieknie od tygodnia. Syfon pod zlewem też do wymiany." });
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details.ShouldNotBeNull();
        details.Description.ShouldBe("Kran cieknie od tygodnia. Syfon pod zlewem też do wymiany.");
        details.Title.ShouldBe("Cieknący kran w kuchni");
        details.Revision.ShouldBe(1);
        details.UpdatedAt.ShouldNotBeNull();
        var change = details.Changes.ShouldHaveSingleItem();
        change.Revision.ShouldBe(1);
        var changedField = change.Fields.ShouldHaveSingleItem();
        changedField.Field.ShouldBe("Description");
        changedField.OldValue.ShouldBe("Kran cieknie od tygodnia, mam nową uszczelkę.");
        changedField.NewValue.ShouldBe("Kran cieknie od tygodnia. Syfon pod zlewem też do wymiany.");

        // Pomocnik widzi przy swojej ofercie, co zmieniło się od chwili jej złożenia: „było” → „jest”.
        var offer = details.Offers.ShouldHaveSingleItem();
        offer.AnnouncementRevision.ShouldBe(0);
        offer.IsOutdated.ShouldBeTrue();
        var sinceOffer = offer.ChangesSinceOffer.ShouldHaveSingleItem();
        sinceOffer.Field.ShouldBe("Description");
        sinceOffer.OldValue.ShouldBe("Kran cieknie od tygodnia, mam nową uszczelkę.");
        sinceOffer.NewValue.ShouldBe("Kran cieknie od tygodnia. Syfon pod zlewem też do wymiany.");

        // Starej oferty nie da się już przyjąć…
        await (await owner.Client.PostEmptyAsync($"{url}/offers/{offerId}/accept"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        // …ale kontroferta powstaje na nowej rewizji i pomocnik może ją zaakceptować.
        var counter = await owner.Client.PostAsJsonAsync($"{url}/offers/{offerId}/counter", TestApi.OfferBody(160m));
        counter.StatusCode.ShouldBe(HttpStatusCode.Created);
        var counterId = (await counter.ReadAsync<CreatedDto>()).Id;
        (await helper.Client.PostEmptyAsync($"{url}/offers/{counterId}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var accepted = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        var acceptedOffer = accepted!.Offers.Single(o => o.Id == counterId);
        acceptedOffer.Status.ShouldBe("Accepted");
        acceptedOffer.AnnouncementRevision.ShouldBe(1);
        acceptedOffer.IsOutdated.ShouldBeFalse();
        acceptedOffer.ChangesSinceOffer.ShouldBeEmpty();
    }

    [Fact]
    public async Task TwoEditsAfterOffer_OfferShowsValueFromOfferTimeAndCurrentValue()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        await helper.MakeOfferAsync(announcementId, 120m);

        (await owner.Client.PatchAsJsonAsync(url, new { budget = new { amount = 180m, currency = "PLN" } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PatchAsJsonAsync(url, new { budget = new { amount = 200m, currency = "PLN" } }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Revision.ShouldBe(2);
        details.Changes.Count.ShouldBe(2);
        var budget = details.Offers.ShouldHaveSingleItem().ChangesSinceOffer.ShouldHaveSingleItem();
        budget.Field.ShouldBe("Budget");
        budget.OldValue.ShouldBe("150.00 PLN");
        budget.NewValue.ShouldBe("200.00 PLN");
    }

    [Fact]
    public async Task LaterEdit_CannotHideEarlierPriceChange()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        await helper.MakeOfferAsync(announcementId, 120m);
        const string original = "Kran cieknie od tygodnia, mam nową uszczelkę.";

        (await owner.Client.PatchAsJsonAsync(url, new
        {
            description = original + " Syfon też do wymiany.",
            budget = new { amount = 500m, currency = "PLN" }
        })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PatchAsJsonAsync(url, new { description = original }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        var offer = details!.Offers.ShouldHaveSingleItem();
        offer.IsOutdated.ShouldBeTrue();
        offer.EditsSinceOffer.ShouldBe(2);
        var budget = offer.ChangesSinceOffer.ShouldHaveSingleItem();
        budget.Field.ShouldBe("Budget");
        budget.OldValue.ShouldBe("150.00 PLN");
        budget.NewValue.ShouldBe("500.00 PLN");
        // Pełna historia nadal pokazuje obie edycje, także cofnięcie opisu.
        details.Changes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Edit_WithSameValues_DoesNotCreateRevision()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        var patch = await owner.Client.PatchAsJsonAsync(url, new { title = "Cieknący kran w kuchni" });
        patch.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Revision.ShouldBe(0);
        details.Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_ByNotOwner_Returns409()
    {
        var owner = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await stranger.Client.PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { title = "Moje ogłoszenie" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Edit_WithoutToken_Returns401()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await _factory.CreateClient().PatchAsJsonAsync($"/api/announcements/{announcementId}",
            new { title = "Bez logowania" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Edit_AfterAcceptance_Returns409()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);
        (await owner.Client.PostEmptyAsync($"{url}/offers/{offerId}/accept")).StatusCode
            .ShouldBe(HttpStatusCode.NoContent);

        var response = await owner.Client.PatchAsJsonAsync(url, new { description = "Zmiana po akceptacji." });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Edit_ZeroBudget_Returns409AndKeepsData()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        var response = await owner.Client.PatchAsJsonAsync(url, new { budget = new { amount = 0m, currency = "PLN" } });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Budget.Amount.ShouldBe(150m);
        details.Revision.ShouldBe(0);
    }
}
