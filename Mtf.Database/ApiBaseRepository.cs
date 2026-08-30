using Microsoft.Extensions.Logging;
using Mtf.Database.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace Mtf.Database;

public abstract class ApiBaseRepository<TEntity, TIdentifierType>(
    HttpClient httpClient,
    ILogger logger,
    string baseEndpoint) : IBaseRepository<TEntity, TIdentifierType>
    where TEntity : class, IHasIdentifier<TIdentifierType>
{
    protected readonly HttpClient httpClient = httpClient;
    protected readonly ILogger logger = logger;
    protected readonly string baseEndpoint = baseEndpoint.TrimEnd('/');

    public virtual async Task<ReadOnlyCollection<TEntity>> GetAllAsync()
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<TEntity>>(baseEndpoint).ConfigureAwait(false);
            return new ReadOnlyCollection<TEntity>(response ?? new List<TEntity>());
        }
        catch (Exception ex)
        {
            logger.Log(ex, "Failed to fetch all entities from {Endpoint}", baseEndpoint);
            return new ReadOnlyCollection<TEntity>(new List<TEntity>());
        }
    }

    /// <summary>
    /// Filters entities via a plain GET with <paramref name="param"/>'s properties encoded as a query
    /// string - never via POST to <see cref="baseEndpoint"/>, which a conventional REST controller
    /// treats as Create. A server only returns a filtered result if it has a matching endpoint that
    /// reads those query parameters (e.g. an ASP.NET Core action inferring a complex parameter as
    /// <c>[FromQuery]</c>); without one, this degrades to returning the full unfiltered list rather
    /// than corrupting data, unlike the previous POST-based implementation.
    /// </summary>
    public virtual async Task<ReadOnlyCollection<TEntity>> GetAllWhereAsync(object param)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<TEntity>>(BuildWhereRequestUri(param)).ConfigureAwait(false);
            return new ReadOnlyCollection<TEntity>(response ?? new List<TEntity>());
        }
        catch (Exception ex)
        {
            logger.Log(ex, "Failed to fetch filtered entities from {Endpoint}", baseEndpoint);
            return new ReadOnlyCollection<TEntity>(new List<TEntity>());
        }
    }

    private string BuildWhereRequestUri(object param)
    {
        var query = string.Join("&", param.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (property.Name, Value: property.GetValue(param)))
            .Where(pair => pair.Value != null)
            .Select(pair => $"{Uri.EscapeDataString(pair.Name)}={Uri.EscapeDataString(Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty)}"));

        return query.Length == 0 ? baseEndpoint : $"{baseEndpoint}?{query}";
    }

    public virtual async Task<TEntity?> GetByIdAsync(TIdentifierType id)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<TEntity>($"{baseEndpoint}/{id}").ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            logger.Log(ex, "HttpRequestException while fetching entity {Id} from {Endpoint}", id, baseEndpoint);
            return null;
        }
    }

    public virtual async Task DeleteAsync(TIdentifierType id)
    {
        var response = await httpClient.DeleteAsync($"{baseEndpoint}/{id}");
        response.EnsureSuccessStatusCode();
    }

    public virtual async Task<TEntity?> InsertAsync(TEntity entity)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(baseEndpoint, entity).ConfigureAwait(false);
            return await HandleResponseAsync(response, entity.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(ex, "{Repository}.InsertAsync failed for Id={Id}: {Message}", GetType().Name, entity.Id, ex.Message);
            throw;
        }
    }

    public virtual async Task<TEntity?> UpdateAsync(TEntity entity)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"{baseEndpoint}/{entity.Id}", entity).ConfigureAwait(false);
            return await HandleResponseAsync(response, entity.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.Log(ex, "{Repository}.UpdateAsync failed for Id={Id}: {Message}", GetType().Name, entity.Id, ex.Message);
            throw;
        }
    }

    protected async Task<TEntity?> HandleResponseAsync(HttpResponseMessage? response, TIdentifierType id)
    {
        if (response == null)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var returned = await response.Content.ReadFromJsonAsync<TEntity>().ConfigureAwait(false) ?? null;
        if (returned != null)
        {
            return returned;
        }

        throw new InvalidOperationException($"Response from {baseEndpoint} did not contain a valid entity for Id={id}");
    }
}