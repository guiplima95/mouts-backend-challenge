using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.WebApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public class SalesWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string DbName = "SalesFunctionalTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<DefaultContext>))
                .ToList();

            foreach (var d in dbDescriptors)
            {
                services.Remove(d);
            }

            services.AddDbContext<DefaultContext>(opts =>
                opts.UseInMemoryDatabase(DbName));

            var eventStoreDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEventStoreRepository));

            if (eventStoreDescriptor is not null)
            {
                services.Remove(eventStoreDescriptor);
            }

            services.AddScoped<IEventStoreRepository>(_ => Substitute.For<IEventStoreRepository>());

            var redisCacheDescriptors = services
                .Where(d => d.ServiceType == typeof(IDistributedCache))
                .ToList();

            foreach (var d in redisCacheDescriptors)
            {
                services.Remove(d);
            }

            services.AddDistributedMemoryCache();
        });
    }
}
