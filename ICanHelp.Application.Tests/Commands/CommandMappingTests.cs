using ICanHelp.Application.Commands;
using ICanHelp.Application.Commands.Handlers;
using ICanHelp.Application.DTO;
using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Application.Tests.Commands;

/// <summary>Wspólne mapowanie danych z komend na obiekty domeny – jedno miejsce dla wszystkich handlerów.</summary>
public class CommandMappingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OptionalText_Empty_MeansNoText(string? text)
        => text.ToOptionalDescription().ShouldBeNull();

    [Fact]
    public void OptionalText_Given_BecomesDescription()
        => "Mogę wpaść po pracy.".ToOptionalDescription().ShouldNotBeNull().Value.ShouldBe("Mogę wpaść po pracy.");

    [Fact]
    public void OptionalText_TooShort_IsInvalid()
        => Should.Throw<InvalidDescriptionException>(() => "a".ToOptionalDescription());

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Choroba.  ", "Choroba.")]
    public void JobExitNote_IsTrimmed_AndEmptyMeansNone(string? note, string? expected)
    {
        var result = new AbandonJob { Note = note }.ToNote();

        (result?.Value).ShouldBe(expected);
    }

    [Fact]
    public void OfferTerms_TakeThePrice_DatesAsUtc_AndTheOptionalMessage()
    {
        var start = new DateTime(2026, 11, 20, 16, 0, 0, DateTimeKind.Unspecified);
        var command = new CounterOffer
        {
            Price = new PriceDto { Amount = 120.5m, Currency = "pln" },
            ProposedStart = start,
            ProposedEnd = start.AddHours(2),
            Message = "  "
        };

        var terms = command.ToTerms();

        terms.Price.ShouldBe(new Price(120.5m, "PLN"));
        terms.ProposedStart.ShouldBe(start);
        terms.ProposedStart.Kind.ShouldBe(DateTimeKind.Utc);
        terms.ProposedEnd.ShouldBe(start.AddHours(2));
        terms.ProposedEnd.Kind.ShouldBe(DateTimeKind.Utc);
        terms.Message.ShouldBeNull();
    }

    [Fact]
    public void OfferTerms_WrongPrice_IsRejectedBeforeAnythingIsLoaded()
    {
        var command = new MakeOffer { Price = new PriceDto { Amount = -1, Currency = "PLN" } };

        Should.Throw<DomainRuleException>(() => command.ToTerms());
    }
}
