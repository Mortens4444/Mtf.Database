using System;

namespace Mtf.Database.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LengthAttribute(int length) : Attribute
{
    public int Length { get; } = length;
}