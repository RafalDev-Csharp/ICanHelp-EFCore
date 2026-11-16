namespace ICanHelp.Infrastructure.Storage;

/// <summary>Section "Storage" in appsettings.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Folder for uploaded images. Relative = from the content root of the Api project.</summary>
    public string ImagesPath { get; set; } = "App_Data/images";
}
