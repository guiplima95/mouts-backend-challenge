using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Ambev.DeveloperEvaluation.IoC.Cache;

internal sealed class RedisSaleCacheService : ISaleCacheService
{
    private readonly IDistributedCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string CacheKey(Guid id) => $"sale:{id}";

    public RedisSaleCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<GetSaleResult?> GetAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(CacheKey(saleId), cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<GetSaleResult>(bytes, JsonOptions);
    }

    public async Task SetAsync(Guid saleId, GetSaleResult result, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result, JsonOptions);
        await _cache.SetAsync(CacheKey(saleId), bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultTtl
        }, cancellationToken);
    }

    public async Task RemoveAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(CacheKey(saleId), cancellationToken);
    }
}
