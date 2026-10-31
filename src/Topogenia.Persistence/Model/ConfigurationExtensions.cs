// ConfigurationExtensions.cs
// Short forms shared by the configurations of the records: codes, texts, languages, references, versions, audit columns.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Topogenia.Domain.Model;

namespace Topogenia.Persistence.Model;

/// <summary>Configuration helpers; the lengths are the ones of the 1.x schema (`db/001_schema.sql`) unless an ADR says otherwise.</summary>
public static class ConfigurationExtensions
{
    /// <summary>Width of a language code: the configured languages are short tags (AR-29), wider than the 1.x `char(2)`.</summary>
    public const int LanguageLength = 10;

    /// <summary>Width of a readable code (`VTE-E001`, 1.x `varchar(20)`).</summary>
    public const int ReadableCodeLength = 20;

    /// <summary>A closed list stored as its code, in an ASCII column of the given width (1.x `varchar(n)`).</summary>
    public static PropertyBuilder<TEnum> Code<T, TEnum>(this EntityTypeBuilder<T> builder, Expression<Func<T, TEnum>> property, int width)
        where T : class
        where TEnum : struct, Enum =>
        builder.Property(property).HasConversion<CodeConverter<TEnum>>().HasMaxLength(width).IsUnicode(false);

    /// <summary>An optional closed list stored as its code.</summary>
    public static PropertyBuilder<TEnum?> Code<T, TEnum>(this EntityTypeBuilder<T> builder, Expression<Func<T, TEnum?>> property, int width)
        where T : class
        where TEnum : struct, Enum =>
        builder.Property(property).HasConversion<CodeConverter<TEnum>>().HasMaxLength(width).IsUnicode(false);

    /// <summary>An entered text of at most <paramref name="length"/> characters (1.x `nvarchar(n)`); null length: unbounded.</summary>
    public static PropertyBuilder<string?> Text<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> property, int? length)
        where T : class =>
        length is { } max ? builder.Property(property).HasMaxLength(max) : builder.Property(property);

    /// <summary>A technical ASCII text: code, kind, language (1.x `varchar(n)`).</summary>
    public static PropertyBuilder<string?> Ascii<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> property, int length)
        where T : class =>
        builder.Property(property).HasMaxLength(length).IsUnicode(false);

    /// <summary>The language of an entered text or of a translation.</summary>
    public static PropertyBuilder<string?> Language<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, string?>> property)
        where T : class => builder.Ascii(property, LanguageLength);

    /// <summary>A decimal with the precision and scale of its 1.x column, written and read at that scale on both engines (ADR 039).</summary>
    public static PropertyBuilder<decimal?> Decimal<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, decimal?>> property, int precision, int scale)
        where T : class =>
        builder.Property(property).HasPrecision(precision, scale).HasConversion(new ScaledDecimalConverter(precision, scale));

    /// <summary>
    /// A required decimal with the precision and scale of its column, written and read at that scale on both engines (ADR
    /// 039). The application gives every value, the database none (no default): the rows written before the column
    /// existed receive theirs from the migration that adds it.
    /// </summary>
    public static PropertyBuilder<decimal> RequiredDecimal<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, decimal>> property, int precision, int scale)
        where T : class =>
        builder.Property(property).HasPrecision(precision, scale).HasConversion(new ScaledDecimalConverter(precision, scale));

    /// <summary>
    /// A foreign key to <typeparamref name="TPrincipal"/>. No cascade anywhere: what depends on a row is removed first by
    /// the C# code (purge, spec 4.1), as in the 1.x.
    /// </summary>
    public static void References<T, TPrincipal>(this EntityTypeBuilder<T> builder, Expression<Func<T, object?>> foreignKey)
        where T : class
        where TPrincipal : class =>
        builder.HasOne<TPrincipal>().WithMany().HasForeignKey(foreignKey).OnDelete(DeleteBehavior.Restrict);

    /// <summary>The integer version of a modifiable record, used as the concurrency token (D06).</summary>
    public static void Versioned<T>(this EntityTypeBuilder<T> builder)
        where T : class, IVersionedRecord =>
        builder.Property(record => record.Version).IsConcurrencyToken();

    /// <summary>`created_by` and `updated_by` name users (1.x `FK_table_created_by`, `FK_table_updated_by`).</summary>
    public static void AuditedBy<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, object?>> createdBy, Expression<Func<T, object?>>? updatedBy)
        where T : class
    {
        builder.References<T, AppUser>(createdBy);
        if (updatedBy is not null)
            builder.References<T, AppUser>(updatedBy);
    }

    /// <summary>A key given by the application (Guid v7, D07): never generated by the database.</summary>
    public static void KeyGivenByTheApplication<T>(this EntityTypeBuilder<T> builder, Expression<Func<T, object?>> key)
        where T : class
    {
        builder.HasKey(key);
        foreach (var property in builder.Metadata.FindPrimaryKey()!.Properties)
            builder.Property(property.Name).ValueGeneratedNever();
    }
}
