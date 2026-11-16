using System.Net.Http.Headers;
using ICanHelp.Api.Tests.Infrastructure;
using ICanHelp.Application.DTO;

namespace ICanHelp.Api.Tests;

/// <summary>Krok 10: upload / pobranie / usunięcie zdjęć, limit, miniatura, nieaktualna oferta po dodaniu zdjęcia.</summary>
[Collection(ApiCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AnnouncementImageTests
{
    // Prawdziwy, najmniejszy możliwy PNG 1×1 – wystarczy, żeby przejść rozpoznawanie formatu.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly ApiFactory _factory;

    public AnnouncementImageTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_ImageIsListedServedAndBecomesThumbnail()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";

        var upload = await UploadAsync(owner.Client, announcementId, Png, "kran.png");
        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        var imageId = (await upload.ReadAsync<CreatedDto>()).Id;
        upload.Headers.Location!.ToString().ShouldEndWith($"{url}/images/{imageId}");

        // Szczegóły (dla każdego, także niezalogowanego): zdjęcie z adresem, nowa rewizja z polem Images.
        var anonymous = _factory.CreateClient();
        var details = await anonymous.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details.ShouldNotBeNull();
        var image = details.Images.ShouldHaveSingleItem();
        image.Id.ShouldBe(imageId);
        image.FileName.ShouldBe("kran.png");
        image.FileExtension.ShouldBe(".png");
        image.Url.ShouldBe($"{url}/images/{imageId}");
        details.ThumbnailUrl.ShouldBe(image.Url);
        details.Revision.ShouldBe(1);
        var field = details.Changes.ShouldHaveSingleItem().Fields.ShouldHaveSingleItem();
        field.Field.ShouldBe("Images");
        field.OldValue.ShouldBe(string.Empty);
        field.NewValue.ShouldBe(imageId.ToString());

        // Plik: te same bajty, Content-Type z rozpoznanego formatu.
        var file = await anonymous.GetAsync(image.Url);
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await file.Content.ReadAsByteArrayAsync()).ShouldBe(Png);

        // Lista: miniatura bez ładowania wszystkich zdjęć.
        var list = await anonymous.GetFromJsonAsync<List<AnnouncementDto>>($"/api/announcements?ownerId={owner.Id}");
        list.ShouldNotBeNull();
        list.ShouldHaveSingleItem().ThumbnailUrl.ShouldBe(image.Url);
    }

    [Fact]
    public async Task Upload_MakesPendingOfferOutdated()
    {
        var owner = await _factory.CreateUserAsync();
        var helper = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        var offerId = await helper.MakeOfferAsync(announcementId, 120m);

        (await UploadAsync(owner.Client, announcementId, Png, "kran.png")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var details = await helper.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        var offer = details!.Offers.ShouldHaveSingleItem();
        offer.IsOutdated.ShouldBeTrue();
        offer.ChangesSinceOffer.ShouldHaveSingleItem().Field.ShouldBe("Images");

        await (await owner.Client.PostEmptyAsync($"{url}/offers/{offerId}/accept"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Upload_ByNotOwner_IsConflict()
    {
        var owner = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        await (await UploadAsync(stranger.Client, announcementId, Png, "kran.png"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");
    }

    [Fact]
    public async Task Upload_WithoutToken_IsUnauthorized()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        var response = await UploadAsync(_factory.CreateClient(), announcementId, Png, "kran.png");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_NotAnImage_IsBadRequest()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        // Rozszerzenie .jpg nic nie daje – liczy się zawartość.
        await (await UploadAsync(owner.Client, announcementId, "to nie jest obrazek"u8.ToArray(), "kran.jpg"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidImage");
    }

    [Fact]
    public async Task Upload_TooLarge_IsBadRequest()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var content = new byte[5 * 1024 * 1024 + 1];
        Png.CopyTo(content, 0);

        await (await UploadAsync(owner.Client, announcementId, content, "duze.png"))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "InvalidImage");
    }

    [Fact]
    public async Task Upload_SixthImage_IsConflict()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        for (var i = 0; i < 5; i++)
        {
            (await UploadAsync(owner.Client, announcementId, Png, $"zdjecie{i}.png")).StatusCode
                .ShouldBe(HttpStatusCode.Created);
        }

        await (await UploadAsync(owner.Client, announcementId, Png, "szoste.png"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>($"/api/announcements/{announcementId}");
        details!.Images.Count.ShouldBe(5);
        details.Images.Select(i => i.FileName).ToArray()
            .ShouldBe(new[] { "zdjecie0.png", "zdjecie1.png", "zdjecie2.png", "zdjecie3.png", "zdjecie4.png" });
        details.ThumbnailUrl.ShouldBe(details.Images[0].Url);
    }

    [Fact]
    public async Task Delete_RemovesImageAndFile()
    {
        var owner = await _factory.CreateUserAsync();
        var stranger = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();
        var url = $"/api/announcements/{announcementId}";
        var imageId = (await (await UploadAsync(owner.Client, announcementId, Png, "kran.png")).ReadAsync<CreatedDto>()).Id;

        await (await stranger.Client.DeleteAsync($"{url}/images/{imageId}"))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "DomainRule");

        (await owner.Client.DeleteAsync($"{url}/images/{imageId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var details = await owner.Client.GetFromJsonAsync<AnnouncementDetailsDto>(url);
        details!.Images.ShouldBeEmpty();
        details.ThumbnailUrl.ShouldBeNull();
        details.Revision.ShouldBe(2);

        await (await owner.Client.GetAsync($"{url}/images/{imageId}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await owner.Client.DeleteAsync($"{url}/images/{imageId}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "ImageNotFound");
    }

    [Fact]
    public async Task GetImage_UnknownImage_IsNotFound()
    {
        var owner = await _factory.CreateUserAsync();
        var announcementId = await owner.CreateAnnouncementAsync();

        await (await owner.Client.GetAsync($"/api/announcements/{announcementId}/images/{Guid.NewGuid()}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid announcementId, byte[] content,
        string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var form = new MultipartFormDataContent { { file, "file", fileName } };
        return client.PostAsync($"/api/announcements/{announcementId}/images", form);
    }
}
