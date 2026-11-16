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
/// Dane testowe do ręcznych testów (konta, ogłoszenia w Augustowie i Suwałkach, oferty, jedno zakończone zlecenie
/// z ocenami). Włączane ustawieniem <c>Seed:DemoData = true</c> (appsettings.Development.json), tylko gdy w bazie nie ma
/// jeszcze żadnego użytkownika. Idzie przez prawdziwe handlery komend – te same reguły i zdarzenia co w aplikacji.
/// Wszystkie konta mają to samo hasło <see cref="Password"/> (tylko dla lokalnej bazy deweloperskiej).
/// </summary>
internal sealed class DemoDataSeeder(IServiceProvider serviceProvider, IConfiguration configuration,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    public const string Password = "Test1234!";

    private static readonly (double Lat, double Lng) Augustow = (53.8436, 22.9790);
    private static readonly (double Lat, double Lng) Suwalki = (54.1115, 22.9308);

    private sealed record DemoUser(string UserName, string FullName, string City, string PostalCode, string Phone,
        string Description)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Email => $"{UserName}@test.pl";
    }

    private sealed record DemoAnnouncement(DemoUser Owner, string Category, string Subcategory, string Title,
        string Description, decimal Budget, (double Lat, double Lng) Near, double OffsetLat, double OffsetLng,
        DifficultyLevel Difficulty = DifficultyLevel.Easy)
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

        var anna = new DemoUser("anna", "Anna Kowalska", "Augustów", "16-300", "500100201",
            "Mieszkam w Augustowie, często potrzebuję pomocy przy domu i ogrodzie.");
        var marek = new DemoUser("marek", "Marek Nowak", "Augustów", "16-300", "500100202",
            "Hydraulik i złota rączka, dojeżdżam codziennie Augustów – Suwałki.");
        var kasia = new DemoUser("kasia", "Katarzyna Wiśniewska", "Suwałki", "16-400", "500100203",
            "Studentka matematyki, udzielam korepetycji i pomagam z komputerem.");
        var piotr = new DemoUser("piotr", "Piotr Zieliński", "Suwałki", "16-400", "500100204",
            "Elektryk z uprawnieniami, po pracy chętnie pomogę w drobnych sprawach.");
        var ewa = new DemoUser("ewa", "Ewa Wójcik", "Suwałki", "16-400", "500100205",
            "Emerytka, lubię ogród i zwierzęta.");
        var tomek = new DemoUser("tomek", "Tomasz Lewandowski", "Augustów", "16-300", "500100206",
            "Mam busa – przeprowadzki, przewóz, wywóz gratów.");
        DemoUser[] users = [anna, marek, kasia, piotr, ewa, tomek];

        DemoAnnouncement[] announcements =
        [
            new(anna, "Hydraulika", "Cieknący kran / spłuczka", "Cieknie kran w kuchni",
                "Kran jednouchwytowy kapie od tygodnia, pewnie do wymiany uszczelka albo głowica.", 80, Augustow, 0.004, -0.006),
            new(anna, "Ogród i podwórko", "Koszenie trawy", "Skoszenie trawnika ok. 300 m²",
                "Mam własną kosiarkę spalinową, potrzebuję kogoś do skoszenia i zgrabienia trawy.", 100, Augustow, -0.003, 0.008),
            new(anna, "Meble i montaż", "Składanie mebli", "Złożenie szafy z IKEA",
                "Szafa PAX 2 m, wszystkie części na miejscu. Najlepiej w weekend.", 150, Augustow, 0.002, 0.003,
                DifficultyLevel.Medium),
            new(ewa, "Porady i pomoc codzienna", "Opieka nad zwierzętami", "Spacery z psem przez tydzień",
                "Wyjeżdżam do sanatorium, szukam kogoś do wyprowadzania spokojnego labradora 2 razy dziennie.", 200, Suwalki, -0.004, 0.005),
            new(ewa, "Ogród i podwórko", "Przycinanie drzew i krzewów", "Przycięcie żywopłotu",
                "Żywopłot z tui ok. 15 m długości, wysokość 1,8 m.", 180, Suwalki, 0.006, -0.004, DifficultyLevel.Medium),
            new(ewa, "Komputery i elektronika", "Pomoc z komputerem / telefonem", "Ustawienie nowego smartfona",
                "Kupiłam nowy telefon – proszę o przeniesienie kontaktów i zdjęć oraz pokazanie WhatsAppa.", 50, Suwalki, -0.002, -0.007),
            new(kasia, "Przeprowadzki i transport", "Pomoc przy przeprowadzce", "Przeprowadzka kawalerki w Suwałkach",
                "Kilkanaście kartonów, łóżko i biurko, 3 piętro bez windy. Przejazd ok. 3 km.", 250, Suwalki, 0.003, 0.009,
                DifficultyLevel.Medium),
            new(kasia, "Auto i rower", "Serwis roweru", "Regulacja przerzutek w rowerze",
                "Rower trekkingowy, przerzutki przeskakują, przydałoby się też nasmarować łańcuch.", 60, Suwalki, -0.006, 0.002),
            new(piotr, "Malowanie i wykończenia", "Malowanie ścian", "Pomalowanie pokoju 12 m²",
                "Ściany po gładzi, farba kupiona. Potrzebna pomoc przy dwóch warstwach i suficie.", 400, Suwalki, 0.008, 0.006,
                DifficultyLevel.Medium),
            new(piotr, "Sprzątanie", "Mycie okien", "Umycie okien przed zimą",
                "6 okien w domu jednorodzinnym, w tym 2 balkonowe.", 150, Suwalki, -0.009, -0.003),
            new(tomek, "Korepetycje i nauka", "Języki obce", "Angielski dla syna – 8 klasa",
                "Przygotowanie do egzaminu ósmoklasisty, 2 razy w tygodniu po godzinie.", 60, Augustow, -0.005, -0.004),
            new(tomek, "Elektryka", "Montaż lamp", "Montaż 3 lamp sufitowych",
                "Lampy kupione, przewody wyprowadzone z sufitu. Wysokość pomieszczeń 2,6 m.", 150, Augustow, 0.007, 0.002),
            new(marek, "Drobne naprawy domowe", "Drzwi, okna, zamki", "Wymiana wkładki w zamku",
                "Zacina się zamek w drzwiach wejściowych, wkładka kupiona.", 50, Augustow, -0.001, 0.010),
            new(anna, "Elektryka", "Gniazdka i włączniki", "Wymiana dwóch gniazdek",
                "Stare gniazdka w łazience do wymiany na hermetyczne.", 100, Augustow, -0.007, 0.001),
            new(kasia, "Hydraulika", "Udrażnianie odpływu", "Zapchany odpływ prysznica",
                "Woda schodzi bardzo wolno, chemia nie pomogła.", 90, Suwalki, 0.001, -0.010),
            new(ewa, "Przeprowadzki i transport", "Wywóz gratów", "Wywóz starej wersalki i szafki",
                "Meble z piwnicy do wywiezienia na PSZOK.", 120, Suwalki, 0.005, 0.001)
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
            }

            await SeedNegotiationsAsync(services, announcements, anna, marek, kasia, piotr, tomek, cancellationToken);
            logger.LogInformation("Demo data seeded: {Users} users (<name>@test.pl), {Announcements} announcements.",
                users.Length, announcements.Length);
        }
        catch (Exception exception)
        {
            // Dane testowe nie mogą zablokować startu API – w razie błędu tylko log.
            logger.LogError(exception, "Seeding demo data failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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

        // Kran Anny: dwie oczekujące oferty (Marek i Piotr) – do porównania i negocjacji.
        await Offer(a[0], marek, 90, 0, "Mam uszczelki i głowice w aucie, mogę jutro po 15.");
        await Offer(a[0], piotr, 70, 1, "Mogę pojutrze, wezmę zestaw uszczelek.");

        // Szafa Anny: oferta Tomka przyjęta → zlecenie „do rozpoczęcia”.
        var wardrobeOffer = await Offer(a[2], tomek, 150, 2, "Składałem już kilka PAX-ów, mam wkrętarkę.");
        await accept.HandleAsync(new AcceptOffer(a[2].Id, wardrobeOffer, anna.Id), ct);

        // Pytanie bez oferty: Kasia pyta Ewę o psa.
        var conversationId = Guid.NewGuid();
        await startConversation.HandleAsync(new StartConversation(a[3].Id, conversationId, kasia.Id), ct);
        await sendMessage.HandleAsync(new SendMessage
        {
            ConversationId = conversationId, MessageId = Guid.NewGuid(), SenderId = kasia.Id,
            Text = "Dzień dobry, czy pies dogaduje się z innymi psami? Mieszkam niedaleko."
        }, ct);

        // Przeprowadzka Kasi: oferta Tomka przyjęta, rozpoczęta, zgłoszona, potwierdzona i oceniona przez obie strony.
        var movingOffer = await Offer(a[6], tomek, 250, 0, "Podjadę busem, we dwóch szybko się uwiniemy.");
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
