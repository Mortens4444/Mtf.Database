using System;
using System.ComponentModel.DataAnnotations;

namespace Mtf.Database.Attributes;

/// <summary>
/// Declares the maximum number of characters a string property may hold in its database column.
/// The repository validates annotated properties before any command is executed and throws
/// <see cref="Mtf.Database.Exceptions.ValueTooLongException"/> for over-long values instead of
/// letting the provider truncate them silently. Trailing spaces beyond the limit are ignored,
/// matching how SQL Server treats them.
/// <para>
/// This attribute derives from <see cref="MaxLengthAttribute"/>, so the same annotation is honoured by
/// ASP.NET Core and Entity Framework validation. The repository also honours the BCL
/// <see cref="MaxLengthAttribute"/>, <see cref="StringLengthAttribute"/> and
/// <see cref="System.ComponentModel.DataAnnotations.LengthAttribute"/> directly, so either can be used.
/// When both <c>Mtf.Database.Attributes</c> and <c>System.ComponentModel.DataAnnotations</c> are imported,
/// use the fully qualified name or prefer <see cref="MaxLengthAttribute"/>.
/// </para>
/// </summary>
/// <param name="length">The maximum number of characters. Values of zero or less disable the check.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LengthAttribute(int length) : MaxLengthAttribute(length)
{
}
