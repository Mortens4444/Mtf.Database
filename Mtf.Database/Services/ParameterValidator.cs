using Dapper;
using Mtf.Database.Exceptions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Mtf.Database.Services;

/// <summary>
/// Validates declared maximum lengths on string properties of a parameter object before a command is executed,
/// so over-long values fail loudly instead of being truncated by the database or a stored procedure.
/// </summary>
internal static partial class ParameterValidator
{
    private sealed record LengthRule(PropertyInfo Property, int MaxLength);

    private static readonly ConcurrentDictionary<Type, LengthRule[]> rulesByType = new();

    private static readonly ConcurrentDictionary<string, HashSet<string>> referencedParametersBySql = new(StringComparer.Ordinal);

    [GeneratedRegex(@"[?@:$]([\p{L}_][\p{L}\p{N}_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterReferenceRegex();

    /// <summary>
    /// Validates <paramref name="param"/> the same way Dapper binds it: dictionaries and
    /// <see cref="SqlMapper.IDynamicParameters"/> are passed through untouched, other collections are
    /// validated element by element (multi-exec) and plain objects are validated property by property.
    /// For text commands only the properties the SQL references are checked; stored procedures bind every property.
    /// </summary>
    /// <exception cref="ValueTooLongException">A string property exceeds its declared maximum length.</exception>
    public static void Validate(string sql, object? param, CommandType commandType = CommandType.Text)
    {
        ArgumentNullException.ThrowIfNull(sql);

        if (param is null or string or SqlMapper.IDynamicParameters or IEnumerable<KeyValuePair<string, object>>)
        {
            return;
        }

        if (param is IEnumerable items)
        {
            foreach (var item in items)
            {
                ValidateObject(sql, item, commandType);
            }
            return;
        }

        ValidateObject(sql, param, commandType);
    }

    private static void ValidateObject(string sql, object? item, CommandType commandType)
    {
        if (item is null)
        {
            return;
        }

        var type = item.GetType();
        var rules = rulesByType.GetOrAdd(type, BuildRules);
        if (rules.Length == 0)
        {
            return;
        }

        var referenced = commandType == CommandType.Text
            ? referencedParametersBySql.GetOrAdd(sql, GetReferencedParameters)
            : null;

        foreach (var rule in rules)
        {
            if (referenced is not null && !referenced.Contains(rule.Property.Name))
            {
                continue;
            }

            if (rule.Property.GetValue(item) is string text && text.AsSpan().TrimEnd(' ').Length > rule.MaxLength)
            {
                throw new ValueTooLongException(type.Name, rule.Property.Name, rule.MaxLength, text.Length);
            }
        }
    }

    private static LengthRule[] BuildRules(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanRead && property.GetIndexParameters().Length == 0 && property.PropertyType == typeof(string))
            .Select(property => new LengthRule(property, GetMaxLength(property)))
            .Where(rule => rule.MaxLength > 0)
            .ToArray();
    }

    private static int GetMaxLength(PropertyInfo property)
    {
        var declared = property.GetCustomAttributes<MaxLengthAttribute>().Select(attribute => attribute.Length)
            .Concat(property.GetCustomAttributes<StringLengthAttribute>().Select(attribute => attribute.MaximumLength))
            .Concat(property.GetCustomAttributes<System.ComponentModel.DataAnnotations.LengthAttribute>().Select(attribute => attribute.MaximumLength))
            .Where(length => length > 0)
            .ToArray();

        return declared.Length == 0 ? 0 : declared.Min();
    }

    private static HashSet<string> GetReferencedParameters(string sql)
    {
        var names = ParameterReferenceRegex().Matches(sql).Select(match => match.Groups[1].Value);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }
}
