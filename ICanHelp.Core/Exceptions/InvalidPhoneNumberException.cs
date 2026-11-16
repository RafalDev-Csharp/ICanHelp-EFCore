namespace ICanHelp.Core.Exceptions;

public class InvalidPhoneNumberException : CustomExceptions
{
    public string PhoneNumber { get; }

    public InvalidPhoneNumberException(string phoneNumber) : base($"Phone number: {phoneNumber} is invalid.")
    {
        PhoneNumber = phoneNumber;
    }
}
