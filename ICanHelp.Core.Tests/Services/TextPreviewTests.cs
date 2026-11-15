using ICanHelp.Core.Services;

namespace ICanHelp.Core.Tests.Services;

/// <summary>Krok 12: skrót dłuższego tekstu (ostatnia wiadomość na liście „moje negocjacje”, powiadomienia).</summary>
public class TextPreviewTests
{
    [Fact]
    public void Short_text_is_unchanged()
    {
        TextPreview.Of("  Będę o 16:00.  ").ShouldBe("Będę o 16:00.");
    }

    [Fact]
    public void Long_text_is_cut_at_a_word_boundary()
    {
        var text = string.Join(' ', Enumerable.Repeat("słowo", 40)); // 239 znaków

        var preview = TextPreview.Of(text);

        preview.ShouldEndWith("słowo…");
        preview.Length.ShouldBeLessThanOrEqualTo(TextPreview.MaxLength + 1);
    }

    [Fact]
    public void One_long_word_is_cut_hard()
    {
        var preview = TextPreview.Of(new string('a', 300));

        preview.ShouldBe(new string('a', TextPreview.MaxLength) + "…");
    }

    [Fact]
    public void Text_of_exactly_the_limit_is_not_cut()
    {
        var text = new string('a', TextPreview.MaxLength);

        TextPreview.Of(text).ShouldBe(text);
    }
}
