using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>A part of a <see cref="Category"/>. Created only through <see cref="Category.AddSubcategory"/>.</summary>
public class Subcategory
{
    public Id Id { get; private set; }
    public Id CategoryId { get; private set; }
    public StringObject SubcategoryName { get; private set; }

    // Required by EF Core.
    private Subcategory()
    {
    }

    internal Subcategory(Id id, Id categoryId, StringObject subcategoryName)
    {
        Id = id;
        CategoryId = categoryId;
        SubcategoryName = subcategoryName;
    }

    internal void Rename(StringObject subcategoryName) => SubcategoryName = subcategoryName;
}
