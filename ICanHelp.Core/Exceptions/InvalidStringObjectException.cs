namespace ICanHelp.Core.Exceptions;

public class InvalidStringObjectException : CustomExceptions
{
    public string StringObject { get; }

    public InvalidStringObjectException(string strObject) : base($"StringObject: {strObject} is incorrect.")
    {
        StringObject = strObject;
    }
}
