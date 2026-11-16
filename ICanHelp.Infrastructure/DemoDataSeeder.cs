using ICanHelp.Application.Abstractions;
using ICanHelp.Application.Commands;
using ICanHelp.Application.DTO;
using ICanHelp.Application.Time;
using ICanHelp.Core.Enums;
using ICanHelp.Infrastructure.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ICanHelp.Infrastructure;

/// <summary>
/// Dane testowe do ręcznych testów (konta, ogłoszenia ze zdjęciami w Łodzi, Warszawie i po drodze – Skierniewice,
/// Łowicz –, oferty, jedno zakończone zlecenie z ocenami). Włączane ustawieniem <c>Seed:DemoData = true</c>
/// (appsettings.Development.json), tylko gdy w bazie nie ma jeszcze żadnego użytkownika. Idzie przez prawdziwe handlery
/// komend – te same reguły i zdarzenia co w aplikacji. Zdjęcia (rysowane ilustracje 800×600) są zasobami osadzonymi
/// z <c>DemoData/Images</c>. Wszystkie konta mają to samo hasło <see cref="Password"/> (tylko dla lokalnej bazy deweloperskiej).
/// </summary>
internal sealed class DemoDataSeeder(IServiceProvider serviceProvider, IConfiguration configuration,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    public const string Password = "Test1234!";

    private static readonly (double Lat, double Lng) Lodz = (51.7592, 19.4560);
    private static readonly (double Lat, double Lng) Warszawa = (52.2297, 21.0122);
    private static readonly (double Lat, double Lng) Skierniewice = (51.9547, 20.1580);
    private static readonly (double Lat, double Lng) Lowicz = (52.1066, 19.9446);

    /// <summary>Prefiks nazw zasobów ze zdjęciami (<c>LogicalName</c> w csproj).</summary>
    private const string ImageResourcePrefix = "DemoImages.";

    private sealed record DemoUser(string UserName, string FullName, string City, string PostalCode, string Phone,
        string Description)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Email => $"{UserName}@test.pl";
    }

    /// <summary><c>Images</c> – nazwy plików z <c>DemoData/Images</c> (bez rozszerzenia), 1–3 na ogłoszenie.</summary>
    private sealed record DemoAnnouncement(DemoUser Owner, string Category, string Subcategory, string Title,
        string Description, decimal Budget, (double Lat, double Lng) Near, double OffsetLat, double OffsetLng,
        string[] Images, DifficultyLevel Difficulty = DifficultyLevel.Easy)
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Seed:DemoData"))
        {
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ICanHelpDbContext>();

        if (!await db.Categories.AnyAsync(cancellationToken) || await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = await db.Categories.AsNoTracking().Include(c => c.Subcategories).ToListAsync(cancellationToken);
        (Guid CategoryId, Guid SubcategoryId) Find(string category, string subcategory)
        {
            var c = categories.Single(x => x.CategoryName.Value == category);
            return (c.Id, c.Subcategories.Single(s => s.SubcategoryName.Value == subcategory).Id);
        }

        var anna = new DemoUser("anna", "Anna Kowalska", "Łódź", "91-001", "500100201",
            "Mieszkam na Bałutach, często potrzebuję pomocy przy domu i ogrodzie.");
        var marek = new DemoUser("marek", "Marek Nowak", "Łódź", "90-001", "500100202",
            "Hydraulik i złota rączka, codziennie dojeżdżam Łódź – Warszawa (A2, przez Łowicz).");
        var kasia = new DemoUser("kasia", "Katarzyna Wiśniewska", "Warszawa", "02-001", "500100203",
            "Studentka matematyki na Politechnice, udzielam korepetycji i pomagam z komputerem.");
        var piotr = new DemoUser("piotr", "Piotr Zieliński", "Warszawa", "00-001", "500100204",
            "Elektryk z uprawnieniami, po pracy chętnie pomogę w drobnych sprawach.");
        var ewa = new DemoUser("ewa", "Ewa Wójcik", "Skierniewice", "96-100", "500100205",
            "Emerytka, lubię ogród i zwierzęta.");
        var tomek = new DemoUser("tomek", "Tomasz Lewandowski", "Łódź", "90-001", "500100206",
            "Mam busa – przeprowadzki, przewóz, wywóz gratów. Często jeżdżę do Warszawy.");
        DemoUser[] users = [anna, marek, kasia, piotr, ewa, tomek];

        // Kolejność ma znaczenie: SeedNegotiationsAsync odwołuje się do a[0], a[2], a[3] i a[6].
        DemoAnnouncement[] announcements =
        [
            new(anna, "Hydraulika", "Cieknący kran / spłuczka", "Cieknie kran w kuchni",
                "Kran jednouchwytowy kapie od tygodnia, pewnie do wymiany uszczelka albo głowica.", 80, Lodz, 0.012, -0.020,
                ["sink-faucet", "sink-drip"]),
            new(anna, "Ogród i podwórko", "Koszenie trawy", "Skoszenie trawnika ok. 300 m²",
                "Trawa po kolana, mam własną kosiarkę spalinową. Potrzebuję kogoś do skoszenia i zgrabienia.", 100, Lodz,
                -0.025, 0.030, ["lawn-overgrown"]),
            new(anna, "Meble i montaż", "Składanie mebli", "Złożenie szafy z IKEA",
                "Szafa PAX 2 m, wszystkie części na miejscu. Najlepiej w weekend.", 150, Lodz, 0.008, 0.012,
                ["wardrobe-boxes"], DifficultyLevel.Medium),
            new(ewa, "Porady i pomoc codzienna", "Opieka nad zwierzętami", "Spacery z psem przez tydzień",
                "Wyjeżdżam do sanatorium, szukam kogoś do wyprowadzania spokojnego labradora 2 razy dziennie.", 200,
                Skierniewice, -0.004, 0.006, ["dog-walk"]),
            new(ewa, "Ogród i podwórko", "Przycinanie drzew i krzewów", "Przycięcie żywopłotu",
                "Żywopłot z tui ok. 15 m długości, wysokość 1,8 m, mocno rozrośnięty.", 180, Skierniewice, 0.006, -0.005,
                ["hedge"], DifficultyLevel.Medium),
            new(ewa, "Komputery i elektronika", "Pomoc z komputerem / telefonem", "Ustawienie nowego smartfona",
                "Kupiłam nowy telefon, stary ma pęknięty ekran – proszę o przeniesienie kontaktów i zdjęć oraz pokazanie WhatsAppa.",
                50, Skierniewice, -0.003, -0.008, ["phones"]),
            new(kasia, "Przeprowadzki i transport", "Pomoc przy przeprowadzce", "Przeprowadzka kawalerki na Mokotowie",
                "Kilkanaście kartonów, łóżko i biurko, 3 piętro bez windy. Przejazd ok. 5 km.", 250, Warszawa, -0.030, 0.000,
                ["moving-boxes", "stairwell"], DifficultyLevel.Medium),
            new(kasia, "Auto i rower", "Serwis roweru", "Regulacja przerzutek w rowerze",
                "Rower trekkingowy, przerzutki przeskakują, przydałoby się też nasmarować łańcuch.", 60, Warszawa, 0.020,
                -0.015, ["bike"]),
            new(piotr, "Malowanie i wykończenia", "Malowanie ścian", "Pomalowanie pokoju 12 m²",
                "Ściany po gładzi, farba kupiona, zacząłem jedną ścianę. Potrzebna pomoc przy dwóch warstwach i suficie.",
                400, Warszawa, 0.030, -0.035, ["room-paint"], DifficultyLevel.Medium),
            new(piotr, "Sprzątanie", "Mycie okien", "Umycie okien przed zimą",
                "6 okien w domu jednorodzinnym w Wawrze, w tym 2 balkonowe. Szyby mocno zabrudzone po remoncie.", 150,
                Warszawa, -0.015, 0.110, ["dirty-window"]),
            new(tomek, "Korepetycje i nauka", "Języki obce", "Angielski dla syna – 8 klasa",
                "Przygotowanie do egzaminu ósmoklasisty, 2 razy w tygodniu po godzinie. Najsłabiej z czasami.", 60, Lodz,
                -0.010, -0.015, ["english-books"]),
            new(tomek, "Elektryka", "Montaż lamp", "Montaż 3 lamp sufitowych",
                "Lampy kupione, przewody wyprowadzone z sufitu. Wysokość pomieszczeń 2,6 m.", 150, Lodz, 0.020, 0.005,
                ["ceiling-wires"]),
            new(marek, "Drobne naprawy domowe", "Drzwi, okna, zamki", "Wymiana wkładki w zamku",
                "Zacina się zamek w drzwiach mieszkania po rodzicach w Łowiczu, wkładka kupiona.", 50, Lowicz, -0.002, 0.004,
                ["door-lock"]),
            new(anna, "Elektryka", "Gniazdka i włączniki", "Wymiana dwóch gniazdek",
                "Stare, przebarwione gniazdka w łazience do wymiany na hermetyczne – jedno jest pęknięte.", 100, Lodz,
                -0.018, -0.005, ["old-socket"]),
            new(kasia, "Hydraulika", "Udrażnianie odpływu", "Zapchany odpływ prysznica",
                "Woda schodzi bardzo wolno i stoi w brodziku, chemia nie pomogła.", 90, Warszawa, 0.005, 0.020,
                ["shower-drain"]),
            new(ewa, "Przeprowadzki i transport", "Wywóz gratów", "Wywóz starej wersalki i szafki",
                "Meble z piwnicy do wywiezienia na PSZOK.", 120, Skierniewice, 0.005, 0.001, ["old-sofa"])
        ];

        try
        {
            var signUp = services.GetRequiredService<ICommandHandler<SignUp>>();
            foreach (var user in users)
            {
                await signUp.HandleAsync(new SignUp
                {
                    UserId = user.Id, UserName = user.UserName, FullName = user.FullName, Email = user.Email,
                    Password = Password, PhoneNumber = user.Phone, Country = "Polska", City = user.City,
                    PostalCode = user.PostalCode, Description = user.Description
                }, cancellationToken);
            }

            var create = services.GetRequiredService<ICommandHandler<CreateAnnouncement>>();
            foreach (var a in announcements)
            {
                var (categoryId, subcategoryId) = Find(a.Category, a.Subcategory);
                await create.HandleAsync(new CreateAnnouncement
                {
                    AnnouncementId = a.Id, OwnerId = a.Owner.Id, CategoryId = categoryId, SubcategoryId = subcategoryId,
                    Difficulty = a.Difficulty, Title = a.Title, Description = a.Description,
                    Budget = new PriceDto { Amount = a.Budget, Currency = "PLN" },
                    Location = new LocationDto { Latitude = a.Near.Lat + a.OffsetLat, Longitude = a.Near.Lng + a.OffsetLng }
                }, cancellationToken);
                // Zdjęcia od razu – później (po akceptacji oferty) domena już na nie nie pozwala.
                await AddImagesAsync(services, a, cancellationToken);
            }

            await SeedNegotiationsAsync(services, announcements, anna, marek, kasia, piotr, tomek, cancellationToken);
            logger.LogInformation(
                "Demo data seeded: {Users} users (<name>@test.pl), {Announcements} announcements, {Images} images.",
                users.Length, announcements.Length, announcements.Sum(a => a.Images.Length));
        }
        catch (Exception exception)
        {
            // Dane testowe nie mogą zablokować startu API – w razie błędu tylko log.
            logger.LogError(exception, "Seeding demo data failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Zdjęcia przez prawdziwy handler – te same reguły co w aplikacji (format, limit, właściciel, <c>Open</c>).</summary>
    private static async Task AddImagesAsync(IServiceProvider services, DemoAnnouncement announcement, CancellationToken ct)
    {
        var addImage = services.GetRequiredService<ICommandHandler<AddAnnouncementImage>>();
        var assembly = typeof(DemoDataSeeder).Assembly;
        foreach (var name in announcement.Images)
        {
            await using var content = assembly.GetManifestResourceStream($"{ImageResourcePrefix}{name}.jpg")
                                      ?? throw new InvalidOperationException($"Missing demo image '{name}.jpg'.");
            await addImage.HandleAsync(new AddAnnouncementImage
            {
                AnnouncementId = announcement.Id, ImageId = Guid.NewGuid(), ActorId = announcement.Owner.Id,
                FileName = $"{name}.jpg", Content = content
            }, ct);
        }
    }

    private static async Task SeedNegotiationsAsync(IServiceProvider services, DemoAnnouncement[] a,
        DemoUser anna, DemoUser marek, DemoUser kasia, DemoUser piotr, DemoUser tomek, CancellationToken ct)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;
        var tomorrow = now.Date.AddDays(1);
        var makeOffer = services.GetRequiredService<ICommandHandler<MakeOffer>>();
        var accept = services.GetRequiredService<ICommandHandler<AcceptOffer>>();
        var sendMessage = services.GetRequiredService<ICommandHandler<SendMessage>>();
        var startConversation = services.GetRequiredService<ICommandHandler<StartConversation>>();

        async Task<Guid> Offer(DemoAnnouncement announcement, DemoUser helper, decimal price, int dayOffset, string message)
        {
            var offerId = Guid.NewGuid();
            var start = tomorrow.AddDays(dayOffset).AddHours(15);
            await makeOffer.HandleAsync(new MakeOffer
            {
                AnnouncementId = announcement.Id, OfferId = offerId, HelperId = helper.Id,
                Price = new PriceDto { Amount = price, Currency = "PLN" },
                ProposedStart = start, ProposedEnd = start.AddHours(2), Message = message
            }, ct);
            return offerId;
        }

        // Kran Anny (Łódź): dwie oczekujące oferty – Marek z Łodzi i Piotr, który bywa w Łodzi – do porównania i negocjacji.
        await Offer(a[0], marek, 90, 0, "Mam uszczelki i głowice w aucie, mogę jutro po 15.");
        await Offer(a[0], piotr, 70, 1, "Będę w Łodzi pojutrze, wezmę zestaw uszczelek.");

        // Szafa Anny: oferta Tomka przyjęta → zlecenie „do rozpoczęcia”.
        var wardrobeOffer = await Offer(a[2], tomek, 150, 2, "Składałem już kilka takich szaf, mam wkrętarkę.");
        await accept.HandleAsync(new AcceptOffer(a[2].Id, wardrobeOffer, anna.Id), ct);

        // Pytanie bez oferty: Kasia (wraca z Warszawy przez Skierniewice) pyta Ewę o psa.
        var conversationId = Guid.NewGuid();
        await startConversation.HandleAsync(new StartConversation(a[3].Id, conversationId, kasia.Id), ct);
        await sendMessage.HandleAsync(new SendMessage
        {
            ConversationId = conversationId, MessageId = Guid.NewGuid(), SenderId = kasia.Id,
            Text = "Dzień dobry, czy pies dogaduje się z innymi psami? W weekendy bywam u rodziców w Skierniewicach."
        }, ct);

        // Przeprowadzka Kasi (Warszawa): oferta Tomka przyjęta, rozpoczęta, zgłoszona, potwierdzona i oceniona przez obie strony.
        var movingOffer = await Offer(a[6], tomek, 250, 0, "I tak jadę busem do Warszawy, we dwóch szybko się uwiniemy.");
        await accept.HandleAsync(new AcceptOffer(a[6].Id, movingOffer, kasia.Id), ct);
        await services.GetRequiredService<ICommandHandler<StartAnnouncement>>()
            .HandleAsync(new StartAnnouncement(a[6].Id, tomek.Id), ct);
        await services.GetRequiredService<ICommandHandler<ReportAnnouncementDone>>()
            .HandleAsync(new ReportAnnouncementDone(a[6].Id, tomek.Id), ct);
        await services.GetRequiredService<ICommandHandler<CompleteAnnouncement>>()
            .HandleAsync(new CompleteAnnouncement(a[6].Id, kasia.Id), ct);

        var rate = services.GetRequiredService<ICommandHandler<RateAnnouncement>>();
        await rate.HandleAsync(new RateAnnouncement
        {
            AnnouncementId = a[6].Id, RatingId = Guid.NewGuid(), RaterId = kasia.Id, Rate = 5,
            Comment = "Szybko, sprawnie i z uśmiechem. Polecam!"
        }, ct);
        await rate.HandleAsync(new RateAnnouncement
        {
            AnnouncementId = a[6].Id, RatingId = Guid.NewGuid(), RaterId = tomek.Id, Rate = 5,
            Comment = "Wszystko spakowane i gotowe, świetna współpraca."
        }, ct);
    }
}
