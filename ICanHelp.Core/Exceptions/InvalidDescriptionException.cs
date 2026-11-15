namespace ICanHelp.Core.Exceptions;

public class InvalidDescriptionException : CustomExceptions
{
    public string Description { get; }

    public InvalidDescriptionException(string description) : base($"Description: {description} is not valid.")
    {
        Description = description;
    }
}
