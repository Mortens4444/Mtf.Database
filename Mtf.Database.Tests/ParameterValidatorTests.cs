using Dapper;
using Mtf.Database.Exceptions;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Xunit;

namespace Mtf.Database.Tests;

public sealed class ParameterValidatorTests
{
    private const string InsertSql = "INSERT INTO T (Name, Notes, Code, Title, Both) VALUES (@Name, @Notes, @code, @Title, @Both)";

    private sealed class Annotated
    {
        [Mtf.Database.Attributes.Length(5)]
        public string? Name { get; set; }

        [MaxLength(4)]
        public string? Notes { get; set; }

        [StringLength(3)]
        public string? Code { get; set; }

        [System.ComponentModel.DataAnnotations.Length(0, 2)]
        public string? Title { get; set; }

        [Mtf.Database.Attributes.Length(10)]
        [MaxLength(6)]
        public string? Both { get; set; }

        [Mtf.Database.Attributes.Length(0)]
        public string? Unlimited { get; set; }

        [Mtf.Database.Attributes.Length(1)]
        public int Number { get; set; }

        [Mtf.Database.Attributes.Length(1)]
        public byte[]? Blob { get; set; }

        [Mtf.Database.Attributes.Length(1)]
        public string this[int index] => "indexer";

        public string? Plain { get; set; }
    }

    private sealed class Unannotated
    {
        public string? Name { get; set; }
    }

    [Fact]
    public void NullStringDictionaryAndDynamicParameters_PassThrough()
    {
        RowRepository.Validate(InsertSql, null);
        RowRepository.Validate(InsertSql, "0123456789");
        RowRepository.Validate(InsertSql, new Dictionary<string, object> { ["Name"] = "0123456789" });
        var dynamic = new DynamicParameters();
        dynamic.Add("Name", "0123456789");
        RowRepository.Validate(InsertSql, dynamic);
    }

    [Fact]
    public void NullSql_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RowRepository.Validate(null!, new Annotated()));
    }

    [Fact]
    public void UnannotatedType_IsNeverRejected()
    {
        RowRepository.Validate(InsertSql, new Unannotated { Name = new string('x', 10_000) });
    }

    [Theory]
    [InlineData("Name", "012345", 5, 6)]
    [InlineData("Notes", "01234", 4, 5)]
    [InlineData("Code", "0123", 3, 4)]
    [InlineData("Title", "012", 2, 3)]
    [InlineData("Both", "0123456", 6, 7)]
    public void EveryLengthAnnotation_IsHonoured(string property, string value, int max, int actual)
    {
        var model = new Annotated();
        typeof(Annotated).GetProperty(property)!.SetValue(model, value);

        var ex = Assert.Throws<ValueTooLongException>(() => RowRepository.Validate(InsertSql, model));

        Assert.Equal(nameof(Annotated), ex.TypeName);
        Assert.Equal(property, ex.PropertyName);
        Assert.Equal(max, ex.MaxLength);
        Assert.Equal(actual, ex.ActualLength);
        Assert.Equal(property, ex.ParamName);
        Assert.Equal($"Annotated.{property} is {actual} characters long, but at most {max} can be stored. (Parameter '{property}')", ex.Message);
    }

    [Fact]
    public void ValuesWithinLimit_Pass()
    {
        RowRepository.Validate(InsertSql, new Annotated { Name = "01234", Notes = "0123", Code = "012", Title = "01", Both = "012345" });
    }

    [Fact]
    public void TrailingSpacesBeyondLimit_AreIgnored()
    {
        RowRepository.Validate(InsertSql, new Annotated { Name = "01234      " });
        Assert.Throws<ValueTooLongException>(() => RowRepository.Validate(InsertSql, new Annotated { Name = "01234 x" }));
    }

    [Fact]
    public void ZeroLengthNonStringAndIndexerProperties_AreIgnored()
    {
        RowRepository.Validate("INSERT INTO T VALUES (@Unlimited, @Number, @Blob, @Item)", new Annotated { Unlimited = new string('x', 100), Number = 123456, Blob = new byte[16] });
    }

    [Fact]
    public void TextCommand_ChecksOnlyReferencedParameters()
    {
        var model = new Annotated { Name = "0123456789", Notes = "0123456789", Code = "x" };

        RowRepository.Validate("UPDATE T SET Code = @Code WHERE Id = @Id", model);
        RowRepository.Validate("UPDATE T SET Code = @CODE", model);
        Assert.Throws<ValueTooLongException>(() => RowRepository.Validate("UPDATE T SET Name = @name", model));
        Assert.Throws<ValueTooLongException>(() => RowRepository.Validate("UPDATE T SET Notes = :Notes", model));
    }

    [Fact]
    public void StoredProcedure_ChecksEveryAnnotatedProperty()
    {
        var model = new Annotated { Notes = "0123456789" };

        var ex = Assert.Throws<ValueTooLongException>(() => RowRepository.Validate("sp_anything", model, CommandType.StoredProcedure));

        Assert.Equal("Notes", ex.PropertyName);
    }

    [Fact]
    public void Collections_AreValidatedElementByElement()
    {
        RowRepository.Validate(InsertSql, new List<Annotated?> { new() { Name = "ok" }, null, new() { Name = "fine" } });

        var ex = Assert.Throws<ValueTooLongException>(() => RowRepository.Validate(InsertSql, new Annotated[] { new() { Name = "ok" }, new() { Name = "0123456789" } }));

        Assert.Equal("Name", ex.PropertyName);
    }

    [Fact]
    public void ValueTooLongException_StandardConstructors()
    {
        var plain = new ValueTooLongException();
        Assert.Null(plain.PropertyName);
        Assert.Equal(0, plain.MaxLength);

        Assert.Equal("m", new ValueTooLongException("m").Message);

        var inner = new InvalidOperationException();
        var withInner = new ValueTooLongException("m", inner);
        Assert.Same(inner, withInner.InnerException);
        Assert.IsAssignableFrom<ArgumentException>(withInner);
    }

    [Fact]
    public void LengthAttribute_IsAMaxLengthAttribute()
    {
        var attribute = new Mtf.Database.Attributes.LengthAttribute(42);

        Assert.Equal(42, attribute.Length);
        Assert.IsAssignableFrom<MaxLengthAttribute>(attribute);
    }
}
