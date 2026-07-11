using Microsoft.Extensions.Logging;
using Mtf.Database.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Mtf.Database;

public abstract class ApiBaseRepository<TEntity, TIdentifierType>(
    HttpClient httpClient,
    ILogger logger,
    string baseEndpoint) : IBaseRepository<TEntity, TIdentifierType>
    where TEntity : class, IHasIdentifier<TIdentifierType>
{
    private readonly HttpClient httpClient = httpClient;
    private readonly ILogger logger = logger;
    private readonly string baseEndpoint = baseEndpoint.TrimEnd('/');

    public virtual async Task<List<TEntity>> GetAllAsync()
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<List<TEntity>>(baseEndpoint).ConfigureAwait(false);
            return response ?? [];
        }
        catch (Exception ex)
        {
            logger.Log(ex, "Failed to fetch all entities from {Endpoint}", baseEndpoint);
            return [];
        }
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