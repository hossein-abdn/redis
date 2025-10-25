using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace RedisApi;

class Program
{
    // Explicit Main method
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // ---- Redis via Connection String ----
        var redisConnStr = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnStr))
        {
            throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");
        }

        var redisConfigurationOptions = ConfigurationOptions.Parse(redisConnStr!);
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfigurationOptions));
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnStr; // تمام تنظیمات ردیس از کانکشن‌استرینگ
        });

        var app = builder.Build();

        // Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI();

        // Endpoints
        app.MapPost("/api/cache/set", async (SetRequest req, IDistributedCache cache, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key))
                return Results.BadRequest(new { error = "key is required" });

            var entryOptions = new DistributedCacheEntryOptions();
            if (req.TtlSeconds is > 0)
                entryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(req.TtlSeconds.Value);

            await cache.SetStringAsync(req.Key, req.Value ?? "", entryOptions, ct);
            return Results.Ok(new { key = req.Key, stored = true, ttlSeconds = req.TtlSeconds });
        })
        .WithName("SetCache")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        app.MapGet("/api/cache/{key}", async (string key, IDistributedCache cache, CancellationToken ct) =>
        {
            var value = await cache.GetStringAsync(key, ct);
            return value is null
                ? Results.NotFound(new { key, found = false })
                : Results.Ok(new { key, value });
        })
        .WithName("GetCache")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/api/cache/getall", async (IConnectionMultiplexer muxer, CancellationToken ct) =>
        {
            var result = new List<string>();

            var endPoint = muxer.GetEndPoints().First();
            Console.WriteLine(endPoint.ToString());
            //RedisKey[] keys = muxer.GetServer(endPoint).Keys(pattern: "*").ToArray();

            //result.AddRange(keys.Select(redisKey => redisKey.ToString()));

            //return value is null
            //    ? Results.NotFound(new { key, found = false })
            //    : Results.Ok(new { key, value });
            return endPoint.ToString();
        })
        .WithName("GetAll")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        app.Run();
    }
}

public record SetRequest(string Key, string Value, int? TtlSeconds);
