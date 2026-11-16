using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Storage;
using ICanHelp.Application.Tests.Fakes;
using ICanHelp.Core.Entities;
using ICanHelp.Core.Entities.Users;
using ICanHelp.Core.Enums;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.Repositories;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Krok 10: dodawanie/usuwanie zdjęć – walidacja pliku, zapis do magazynu, sprzątanie po błędzie.</summary>
public class ImageHandlersTests
{
    // Najkrótsze nagłówki rozpoznawane jako obraz (treść dalej nie ma znaczenia dla walidacji).
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryAnnouncementRepository _announcements = new();
    private readonly InMemoryFileStorage _storage = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FixedClock _clock = new();

    private readonly User _owner;
    private readonly User _stranger;
    private readonly Announcement _announcement;

    public ImageHandlersTests()
    {
        _owner = AddUser("jan.kowalski", "jan@example.com");
        _stranger = AddUser("obcy", "obcy@example.com");

        _announcement = new Announcement(Id.New(), _owner.Id, Id.New(), Id.New(), DifficultyLevel.Easy,
            "Cieknący kran", "Kran w kuchni cieknie od tygodnia.", Price.Pln(150), new Location(50.06, 19.94),
            _clock.UtcNow.AddDays(-1));
        _announcements.Announcements.Add(_announcement);
    }

    // ---------- dodawanie ----------

    [Theory]
    [InlineData("jpeg", ".jpg")]
    [InlineData("png", ".png")]
    [InlineData("webp", ".webp")]
    public async Task Add_AllowedFormat_SavesFileAndAddsImage(string format, string expectedExtension)
    {
        var content = format switch { "jpeg" => Jpeg, "png" => Png, _ => Webp };
        var command = Command(_owner.Id, content, "zdjecie.bin");

        await AddHandler().HandleAsync(command);

        var image = _announcement.Images.ShouldHaveSingleItem();
        image.Id.Value.ShouldBe(command.ImageId);
        image.FileExtension.Value.ShouldBe(expectedExtension);
        image.FileName.Value.ShouldBe("zdjecie.bin");
        image.AddedAt.ShouldBe(_clock.UtcNow);
        image.FilePath.Value.ShouldBe($"{_announcement.Id.Value:N}/{command.ImageId:N}{expectedExtension}");
        _storage.Files[image.FilePath].ShouldBe(content);
        _announcement.Revision.ShouldBe(1);
        _announcement.Changes.ShouldHaveSingleItem().ChangedFields.ShouldBe(AnnouncementFields.Images);
    }

    [Fact]
    public async Task Add_FileNameWithClientPath_KeepsOnlyTheName()
    {
        await AddHandler().HandleAsync(Command(_owner.Id, Jpeg, @"C:\fakepath\kran w kuchni.jpg"));

        _announcement.Images.ShouldHaveSingleItem().FileName.Value.ShouldBe("kran w kuchni.jpg");
    }

    [Fact]
    public async Task Add_VeryLongFileName_IsShortenedKeepingExtension()
    {
        await AddHandler().HandleAsync(Command(_owner.Id, Jpeg, new string('a', 300) + ".jpg"));

        var name = _announcement.Images.ShouldHaveSingleItem().FileName.Value;
        name.Length.ShouldBe(100);
        name.ShouldEndWith(".jpg");
    }

    [Fact]
    public async Task Add_NotAnImage_ThrowsInvalidImage()
    {
        // Nazwa i rozszerzenie wyglądają dobrze, ale zawartość to tekst.
        var command = Command(_owner.Id, "to nie jest obrazek"u8.ToArray(), "kran.jpg");

        await Should.ThrowAsync<InvalidImageException>(() => AddHandler().HandleAsync(command));

        _announcement.Images.ShouldBeEmpty();
        _storage.Files.ShouldBeEmpty();
        _unitOfWork.Executions.ShouldBe(0);
    }

    [Fact]
    public async Task Add_EmptyFile_ThrowsInvalidImage()
    {
        await Should.ThrowAsync<InvalidImageException>(() =>
            AddHandler().HandleAsync(Command(_owner.Id, [], "kran.jpg")));
    }

    [Fact]
    public async Task Add_TooLarge_ThrowsInvalidImage()
    {
        var content = new byte[ImageFiles.MaxBytes + 1];
        Jpeg.CopyTo(content, 0);

        await Should.ThrowAsync<InvalidImageException>(() =>
            AddHandler().HandleAsync(Command(_owner.Id, content, "duze.jpg")));

        _storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_ExactlyMaxSize_IsAccepted()
    {
        var content = new byte[ImageFiles.MaxBytes];
        Jpeg.CopyTo(content, 0);

        await AddHandler().HandleAsync(Command(_owner.Id, content, "duze.jpg"));

        _announcement.Images.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Add_ByNotOwner_ThrowsAndDoesNotSaveFile()
    {
        await Should.ThrowAsync<DomainRuleException>(() =>
            AddHandler().HandleAsync(Command(_stranger.Id, Jpeg, "kran.jpg")));

        _announcement.Images.ShouldBeEmpty();
        _storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_LockedOwner_Throws()
    {
        _owner.Lock();

        await Should.ThrowAsync<UserLockedException>(() =>
            AddHandler().HandleAsync(Command(_owner.Id, Jpeg, "kran.jpg")));

        _storage.Files.ShouldBeEmpty();
    }

    [Fact]
    public async Task Add_UnknownAnnouncement_Throws()
    {
        await Should.ThrowAsync<AnnouncementNotFoundException>(() =>
            AddHandler().HandleAsync(Command(_owner.Id, Jpeg, "kran.jpg") with { AnnouncementId = Guid.NewGuid() }));
    }

    [Fact]
    public async Task Add_SaveToDatabaseFails_DeletesTheSavedFile()
    {
        var handler = AddHandler(new FailingUnitOfWork());

        await Should.ThrowAsync<InvalidOperationException>(() =>
            handler.HandleAsync(Command(_owner.Id, Jpeg, "kran.jpg")));

        _storage.Files.ShouldBeEmpty();
    }

    // ---------- usuwanie ----------

    [Fact]
    public async Task Remove_ByOwner_RemovesImageAndDeletesFile()
    {
        var add = Command(_owner.Id, Jpeg, "kran.jpg");
        await AddHandler().HandleAsync(add);
        var path = _announcement.Images.Single().FilePath.Value;

        await RemoveHandler().HandleAsync(new RemoveAnnouncementImage(_announcement.Id, add.ImageId, _owner.Id));

        _announcement.Images.ShouldBeEmpty();
        _announcement.Revision.ShouldBe(2);
        _storage.Files.ShouldNotContainKey(path);
    }

    [Fact]
    public async Task Remove_ByNotOwner_KeepsImageAndFile()
    {
        var add = Command(_owner.Id, Jpeg, "kran.jpg");
        await AddHandler().HandleAsync(add);

        await Should.ThrowAsync<DomainRuleException>(() =>
            RemoveHandler().HandleAsync(new RemoveAnnouncementImage(_announcement.Id, add.ImageId, _stranger.Id)));

        _announcement.Images.ShouldHaveSingleItem();
        _storage.Files.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Remove_UnknownImage_ThrowsNotFound()
    {
        await Should.ThrowAsync<ImageNotFoundException>(() =>
            RemoveHandler().HandleAsync(new RemoveAnnouncementImage(_announcement.Id, Guid.NewGuid(), _owner.Id)));
    }

    private AddAnnouncementImageHandler AddHandler(IUnitOfWork? unitOfWork = null)
        => new(_announcements, _users, unitOfWork ?? _unitOfWork, _storage, _clock);

    private RemoveAnnouncementImageHandler RemoveHandler() => new(_announcements, _unitOfWork, _storage, _clock);

    private AddAnnouncementImage Command(Guid actorId, byte[] content, string fileName) => new()
    {
        AnnouncementId = _announcement.Id,
        ImageId = Guid.NewGuid(),
        ActorId = actorId,
        FileName = fileName,
        Content = new MemoryStream(content)
    };

    private User AddUser(string userName, string email)
    {
        var user = new User(UserId.New(), userName, "Test User", FakePasswordManager.Prefix + "Haslo123!",
            new ContactData(email, "+48600100200", "Polska", "Kraków", "30-001"), _clock.UtcNow);
        _users.Users.Add(user);
        return user;
    }
}
