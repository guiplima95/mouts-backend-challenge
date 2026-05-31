using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.IoC.Cache;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.EventStore;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();

        var mongoConnectionString = builder.Configuration.GetConnectionString("MongoConnection")
            ?? "mongodb://developer:ev%40luAt10n@localhost:27017";
        var mongoDatabaseName = builder.Configuration["MongoSettings:DatabaseName"]
            ?? "developer_evaluation_events";

        builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
        builder.Services.AddScoped<IMongoDatabase>(provider =>
            provider.GetRequiredService<IMongoClient>().GetDatabase(mongoDatabaseName));
        builder.Services.AddScoped<IEventStoreRepository, MongoEventStoreRepository>();

        var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "sales:";
            });
        }
        else
        {
            builder.Services.AddDistributedMemoryCache();
        }

        builder.Services.AddScoped<ISaleCacheService, RedisSaleCacheService>();
    }
}
