using ICanHelp.Application.DTO;

namespace ICanHelp.Application.Tests.Queries;

/// <summary>Krok 12: skrót ostatniej wiadomości na liście „moje negocjacje”.</summary>
public class MyConversationPreviewTests
{
    [Fact]
    public void Short_text_is_unchanged()
    {
        MyConversationDto.ToPreview("  Będę o 16:00.  ").ShouldBe("Będę o 16:00.");
    }

    [Fact]
    public void Long_text_is_cut_at_a_word_boundary()
    {
        var text = string.Join(' ', Enumerable.Repeat("słowo", 40)); // 239 znaków

        var preview = MyConversationDto.ToPreview(text);

        preview.ShouldEndWith("słowo…");
        preview.Length.ShouldBeLessThanOrEqualTo(MyConversationDto.PreviewLength + 1);
    }

    [Fact]
    public void One_long_word_is_cut_hard()
    {
        var preview = MyConversationDto.ToPreview(new string('a', 300));

        preview.ShouldBe(new string('a', MyConversationDto.PreviewLength) + "…");
    }
}
