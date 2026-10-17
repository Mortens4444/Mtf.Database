using System;

namespace Mtf.Database.Exceptions;

/// <summary>
/// Thrown before a command is executed when a string property exceeds the maximum length
/// declared on it (see <see cref="Attributes.LengthAttribute"/>).
/// </summary>
public class ValueTooLongException : ArgumentException
{
    public string? TypeName { get; }

    public string? PropertyName { get; }

    public int MaxLength { get; }

    public int ActualLength { get; }

    public ValueTooLongException() { }

    public ValueTooLongException(string message) : base(message)
    {
    }

    public ValueTooLongException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ValueTooLongException(string typeName, string propertyName, int maxLength, int actualLength)
        : base($"{typeName}.{propertyName} is {actualLength} characters long, but at most {maxLength} can be stored.", propertyName)
    {
        TypeName = typeName;
        PropertyName = propertyName;
        MaxLength = maxLength;
        ActualLength = actualLength;
    }
}
