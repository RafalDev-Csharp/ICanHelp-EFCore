using ICanHelp.Core.Exceptions;

namespace ICanHelp.Application.Exceptions;

/// <summary>The uploaded file is not an allowed image (JPEG/PNG/WebP), is empty or too large (→ 400).</summary>
public sealed class InvalidImageException : CustomExceptions
{
    public InvalidImageException(string message) : base(message)
    {
    }
}
