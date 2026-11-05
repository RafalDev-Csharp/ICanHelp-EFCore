using ICanHelp.Core.Exceptions;
using ICanHelp.Core.ValueObjects;

namespace ICanHelp.Core.Entities;

/// <summary>Aggregate root: a category of problems (e.g. "Plumbing") with its subcategories.</summary>
public class Category
{
    private readonly List<Subcategory> _subcategories = new();

    public Id Id { get; private set; }
    public StringObject CategoryName { get; private set; }

    public IReadOnlyCollection<Subcategory> Subcategories => _subcategories.AsReadOnly();

    // Required by EF Core.
    private Category()
    {
    }

    public Category(Id id, StringObject categoryName)
    {
        Id = id;
        CategoryName = categoryName;
    }

    public void Rename(StringObject categoryName) => CategoryName = categoryName;

    public Subcategory AddSubcategory(Id subcategoryId, StringObject subcategoryName)
    {
        if (FindByName(subcategoryName) is not null)
        {
            throw new DomainRuleException($"Subcategory: {subcategoryName} already exists in this category.");
        }

        var subcategory = new Subcategory(subcategoryId, Id, subcategoryName);
        _subcategories.Add(subcategory);
        return subcategory;
    }

    public void RenameSubcategory(Id subcategoryId, StringObject subcategoryName)
    {
        var subcategory = GetSubcategory(subcategoryId);
        var sameName = FindByName(subcategoryName);

        if (sameName is not null && sameName.Id != subcategoryId)
        {
            throw new DomainRuleException($"Subcategory: {subcategoryName} already exists in this category.");
        }

        subcategory.Rename(subcategoryName);
    }

    public bool HasSubcategory(Id subcategoryId) => _subcategories.Any(s => s.Id == subcategoryId);

    public void RemoveSubcategory(Id subcategoryId)
        => _subcategories.Remove(GetSubcategory(subcategoryId));

    private Subcategory GetSubcategory(Id subcategoryId)
        => _subcategories.FirstOrDefault(s => s.Id == subcategoryId)
            ?? throw new DomainRuleException($"Subcategory with id: {subcategoryId} does not belong to this category.");

    private Subcategory? FindByName(StringObject name)
        => _subcategories.FirstOrDefault(s =>
            string.Equals(s.SubcategoryName.Value, name.Value, StringComparison.OrdinalIgnoreCase));
}
