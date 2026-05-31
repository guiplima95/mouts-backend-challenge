using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Ambev.DeveloperEvaluation.Functional.Sales;

public class SalesEndpointsTests : IClassFixture<SalesWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SalesEndpointsTests(SalesWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object ValidCreateRequest(string saleNumber = "SALE-001", int quantity = 4) => new
    {
        saleNumber,
        saleDate = DateTime.UtcNow,
        customerId = Guid.NewGuid(),
        customerName = "Test Customer",
        branchId = Guid.NewGuid(),
        branchName = "Test Branch",
        items = new[]
        {
            new
            {
                productId = Guid.NewGuid(),
                productName = "Product A",
                quantity,
                unitPrice = 10.00m
            }
        }
    };

    [Fact]
    public async Task CreateSale_WithValidData_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Sale created successfully");
    }

    [Fact]
    public async Task CreateSale_WithMissingFields_Returns400()
    {
        var invalid = new { saleNumber = "" };

        var response = await _client.PostAsJsonAsync("/api/sales", invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSale_WithMoreThan20Items_Returns422()
    {
        var request = new
        {
            saleNumber = "SALE-OVER-LIMIT",
            saleDate = DateTime.UtcNow,
            customerId = Guid.NewGuid(),
            customerName = "Test Customer",
            branchId = Guid.NewGuid(),
            branchName = "Test Branch",
            items = new[]
            {
                new
                {
                    productId = Guid.NewGuid(),
                    productName = "Bulk Product",
                    quantity = 21,
                    unitPrice = 5.00m
                }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/sales", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetSale_WithExistingId_Returns200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-GET-001"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        var response = await _client.GetAsync($"/api/sales/{saleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SALE-GET-001");
    }

    [Fact]
    public async Task GetSale_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/api/sales/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetSales_Returns200WithPagination()
    {
        await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-LIST-001"));
        await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-LIST-002"));

        var response = await _client.GetAsync("/api/sales?_page=1&_size=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Sales retrieved successfully");
    }

    [Fact]
    public async Task UpdateSale_WithValidData_Returns200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-UPD-001"));
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        var updateRequest = new
        {
            saleNumber = "SALE-UPD-001-UPDATED",
            saleDate = DateTime.UtcNow,
            customerId = Guid.NewGuid(),
            customerName = "Updated Customer",
            branchId = Guid.NewGuid(),
            branchName = "Updated Branch",
            items = new[]
            {
                new
                {
                    productId = Guid.NewGuid(),
                    productName = "Updated Product",
                    quantity = 5,
                    unitPrice = 15.00m
                }
            }
        };

        var response = await _client.PutAsJsonAsync($"/api/sales/{saleId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("SALE-UPD-001-UPDATED");
    }

    [Fact]
    public async Task DeleteSale_WithExistingId_Returns200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-DEL-001"));
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        var response = await _client.DeleteAsync($"/api/sales/{saleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/sales/{saleId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteSale_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/sales/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelSale_WithExistingId_Returns200()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-CANCEL-001"));
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        var response = await _client.PatchAsync($"/api/sales/{saleId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Sale cancelled successfully");
    }

    [Fact]
    public async Task CancelSale_AlreadyCancelled_Returns409()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-CANCEL-DUP"));
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        await _client.PatchAsync($"/api/sales/{saleId}/cancel", null);
        var response = await _client.PatchAsync($"/api/sales/{saleId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CancelSaleItem_WithValidIds_Returns200()
    {
        var productId = Guid.NewGuid();
        var request = new
        {
            saleNumber = "SALE-ITEM-CANCEL-001",
            saleDate = DateTime.UtcNow,
            customerId = Guid.NewGuid(),
            customerName = "Test Customer",
            branchId = Guid.NewGuid(),
            branchName = "Test Branch",
            items = new[]
            {
                new { productId, productName = "Item A", quantity = 5, unitPrice = 10.00m },
                new { productId = Guid.NewGuid(), productName = "Item B", quantity = 4, unitPrice = 8.00m }
            }
        };

        var createResponse = await _client.PostAsJsonAsync("/api/sales", request);
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        var getResponse = await _client.GetAsync($"/api/sales/{saleId}");
        var sale = await ParseBody<ApiDataResponse>(getResponse);
        var itemId = sale!.Data!.GetProperty("items")[0].GetProperty("id").GetString();

        var response = await _client.PatchAsync($"/api/sales/{saleId}/items/{itemId}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Sale item cancelled successfully");
    }

    [Fact]
    public async Task GetSale_AfterCachingAndUpdate_ReturnsFreshData()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/sales", ValidCreateRequest("SALE-CACHE-001"));
        var created = await ParseBody<ApiDataResponse>(createResponse);
        var saleId = created!.Data!.GetProperty("id").GetString();

        await _client.GetAsync($"/api/sales/{saleId}");

        var updateRequest = new
        {
            saleNumber = "SALE-CACHE-001-FRESH",
            saleDate = DateTime.UtcNow,
            customerId = Guid.NewGuid(),
            customerName = "Fresh Customer",
            branchId = Guid.NewGuid(),
            branchName = "Fresh Branch",
            items = new[]
            {
                new { productId = Guid.NewGuid(), productName = "Fresh Product", quantity = 4, unitPrice = 20.00m }
            }
        };

        await _client.PutAsJsonAsync($"/api/sales/{saleId}", updateRequest);

        var getResponse = await _client.GetAsync($"/api/sales/{saleId}");
        var body = await getResponse.Content.ReadAsStringAsync();

        body.Should().Contain("SALE-CACHE-001-FRESH");
    }

    private async Task<T?> ParseBody<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    private sealed class ApiDataResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public JsonElement Data { get; set; }
    }
}
