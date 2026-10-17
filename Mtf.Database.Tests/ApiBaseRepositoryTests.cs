using Microsoft.Extensions.Logging;
using Mtf.Database.Interfaces;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Mtf.Database.Tests;

public class ApiRow : IHasIdentifier<int>
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

internal sealed class ApiRowRepository(HttpClient httpClient, ILogger logger, string baseEndpoint)
    : ApiBaseRepository<ApiRow, int>(httpClient, logger, baseEndpoint)
{
    public Task<ApiRow?> Handle(HttpResponseMessage? response, int id) => HandleResponseAsync(response, id);
}

public sealed class ApiBaseRepositoryTests : IDisposable
{
    private readonly FakeHttpHandler handler = new();
    private readonly FakeLogger<ApiRowRepository> logger = new();
    private readonly HttpClient client;
    private readonly ApiRowRepository repo;

    public ApiBaseRepositoryTests()
    {
        client = new HttpClient(handler);
        repo = new ApiRowRepository(client, logger, "http://api.test/rows/");
    }

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = JsonContent.Create(value) };

    [Fact]
    public async Task GetAllAsync_ReturnsEntities()
    {
        handler.Respond = _ => Json(new[] { new ApiRow { Id = 1, Name = "a" }, new ApiRow { Id = 2 } });

        var all = await repo.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal("http://api.test/rows", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAllAsync_NullBody_ReturnsEmpty()
    {
        handler.Respond = _ => Json<List<ApiRow>?>(null);

        Assert.Empty(await repo.GetAllAsync());
    }

    [Fact]
    public async Task GetAllAsync_Failure_IsLoggedAndReturnsEmpty()
    {
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        Assert.Empty(await repo.GetAllAsync());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("http://api.test/rows", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAllWhereAsync_EncodesPropertiesAsQueryString()
    {
        handler.Respond = _ => Json(new[] { new ApiRow { Id = 1 } });

        var filtered = await repo.GetAllWhereAsync(new { Name = "a b&c", Skip = (string?)null, Page = 2 });

        Assert.Single(filtered);
        Assert.Equal("http://api.test/rows?Name=a%20b%26c&Page=2", handler.Requests[0].RequestUri!.OriginalString);
    }

    [Fact]
    public async Task GetAllWhereAsync_NoValues_UsesBaseEndpoint()
    {
        handler.Respond = _ => Json(Array.Empty<ApiRow>());

        await repo.GetAllWhereAsync(new { Skip = (string?)null });

        Assert.Equal("http://api.test/rows", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task GetAllWhereAsync_Failure_IsLoggedAndReturnsEmpty()
    {
        handler.Respond = _ => throw new HttpRequestException("down");

        Assert.Empty(await repo.GetAllWhereAsync(new { Id = 1 }));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntityOrNullOnHttpFailure()
    {
        handler.Respond = _ => Json(new ApiRow { Id = 5, Name = "five" });
        Assert.Equal("five", (await repo.GetByIdAsync(5))!.Name);
        Assert.Equal("http://api.test/rows/5", handler.Requests[0].RequestUri!.ToString());

        handler.Respond = _ => throw new HttpRequestException("down");
        Assert.Null(await repo.GetByIdAsync(6));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteAndChecksStatus()
    {
        await repo.DeleteAsync(3);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("http://api.test/rows/3", handler.Requests[0].RequestUri!.ToString());

        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        await Assert.ThrowsAsync<HttpRequestException>(() => repo.DeleteAsync(4));
    }

    [Fact]
    public async Task InsertAsync_PostsAndReturnsEntity()
    {
        handler.Respond = _ => Json(new ApiRow { Id = 9, Name = "new" });

        var result = await repo.InsertAsync(new ApiRow { Name = "new" });

        Assert.Equal(9, result!.Id);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    [Fact]
    public async Task InsertAsync_Failure_IsLoggedAndRethrown()
    {
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.InsertAsync(new ApiRow { Id = 1 }));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("ApiRowRepository.InsertAsync failed for Id=1", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InsertAsync_EmptyBody_Throws()
    {
        handler.Respond = _ => Json<ApiRow?>(null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.InsertAsync(new ApiRow { Id = 1 }));
    }

    [Fact]
    public async Task UpdateAsync_PutsAndReturnsEntity()
    {
        handler.Respond = _ => Json(new ApiRow { Id = 2, Name = "upd" });

        var result = await repo.UpdateAsync(new ApiRow { Id = 2, Name = "upd" });

        Assert.Equal("upd", result!.Name);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal("http://api.test/rows/2", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task UpdateAsync_Failure_IsLoggedAndRethrown()
    {
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Conflict);

        await Assert.ThrowsAsync<HttpRequestException>(() => repo.UpdateAsync(new ApiRow { Id = 2 }));
        Assert.Single(logger.Entries);
    }

    [Fact]
    public async Task HandleResponseAsync_NullResponse_ReturnsNull()
    {
        Assert.Null(await repo.Handle(null, 1));
    }
}
