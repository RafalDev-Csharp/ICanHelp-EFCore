using ICanHelp.Application.Exceptions;
using ICanHelp.Application.Queries;

namespace ICanHelp.Application.Tests.Queries;

/// <summary>F11 B: fraza wyszukiwania → wzorzec LIKE. Samo zapytanie SQL sprawdza test integracyjny (PhraseSearchTests).</summary>
public class GetAnnouncementsPhraseTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_phrase_means_no_filter(string? phrase)
    {
        new GetAnnouncements { Phrase = phrase }.GetPhrasePattern().ShouldBeNull();
    }

    [Fact]
    public void Phrase_is_trimmed_and_wrapped_in_wildcards()
    {
        new GetAnnouncements { Phrase = "  kran " }.GetPhrasePattern().ShouldBe("%kran%");
    }

    [Fact]
    public void Wildcards_in_phrase_are_escaped()
    {
        new GetAnnouncements { Phrase = @"50%_[a]\b" }.GetPhrasePattern().ShouldBe(@"%50\%\_\[a]\\b%");
    }

    [Fact]
    public void Max_length_phrase_is_allowed()
    {
        var phrase = new string('a', GetAnnouncements.MaxPhraseLength);

        new GetAnnouncements { Phrase = phrase }.GetPhrasePattern().ShouldBe($"%{phrase}%");
    }

    [Fact]
    public void Too_long_phrase_throws()
    {
        var query = new GetAnnouncements { Phrase = new string('a', GetAnnouncements.MaxPhraseLength + 1) };

        Should.Throw<InvalidSearchPhraseException>(() => query.GetPhrasePattern());
    }
}
