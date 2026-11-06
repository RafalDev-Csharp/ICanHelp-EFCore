namespace ICanHelp.Core.Exceptions;

public class ImageNotFoundException : CustomExceptions
{
    public Guid ImageId { get; }

    public ImageNotFoundException(Guid imageId) : base($"Image with id: {imageId} was not found.")
    {
        ImageId = imageId;
    }
}
