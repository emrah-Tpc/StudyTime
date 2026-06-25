using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using StudyTime;
using StudyTime.Application.Exceptions;

namespace StudyTime.Infrastructure.Tests;

/// <summary>
/// F18 — Merkezi hata yönetimi: istisna tipi → HTTP kodu eşlemesi ve
/// beklenmeyen istisnalarda iç mesajın SIZMADIĞI doğrulanır.
/// </summary>
public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task Maps_DataConflict_To_409()
        => Assert.Equal(StatusCodes.Status409Conflict, await HandleAsync(new DataConflictException("çakışma")));

    [Fact]
    public async Task Maps_KeyNotFound_To_404()
        => Assert.Equal(StatusCodes.Status404NotFound, await HandleAsync(new KeyNotFoundException("yok")));

    [Fact]
    public async Task Maps_InvalidOperation_To_400()
        => Assert.Equal(StatusCodes.Status400BadRequest, await HandleAsync(new InvalidOperationException("kural")));

    [Fact]
    public async Task Maps_UnknownException_To_500()
        => Assert.Equal(StatusCodes.Status500InternalServerError, await HandleAsync(new Exception("infra")));

    [Fact]
    public async Task UnknownException_DoesNotLeakInternalMessage()
    {
        const string secretDetail = "SECRET-DB-CONNECTION-STRING";
        var (status, body) = await HandleWithBodyAsync(new Exception(secretDetail));

        Assert.Equal(StatusCodes.Status500InternalServerError, status);
        Assert.DoesNotContain(secretDetail, body);
    }

    private static async Task<int> HandleAsync(Exception ex)
    {
        var (status, _) = await HandleWithBodyAsync(ex);
        return status;
    }

    private static async Task<(int Status, string Body)> HandleWithBodyAsync(Exception ex)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var ctx = new DefaultHttpContext();
        using var stream = new MemoryStream();
        ctx.Response.Body = stream;

        await handler.TryHandleAsync(ctx, ex, CancellationToken.None);

        stream.Position = 0;
        var body = await new StreamReader(stream).ReadToEndAsync();
        return (ctx.Response.StatusCode, body);
    }
}
