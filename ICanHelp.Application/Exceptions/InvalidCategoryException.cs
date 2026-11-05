using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>Kategoria nie istnieje albo podkategoria do niej nie należy (błędne dane → 400).</summary>
public class InvalidCategoryException : CustomExceptions
{
    public Guid CategoryId { get; }
    public Guid SubcategoryId { get; }

    public InvalidCategoryException(Guid categoryId, Guid subcategoryId)
        : base($"Subcategory: {subcategoryId} does not exist in category: {categoryId}.")
    {
        CategoryId = categoryId;
        SubcategoryId = subcategoryId;
    }
}
