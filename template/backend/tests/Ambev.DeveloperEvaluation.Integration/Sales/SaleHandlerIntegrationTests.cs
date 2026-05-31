using Ambev.DeveloperEvaluation.Application.Sales.CancelSale;
using Ambev.DeveloperEvaluation.Application.Sales.CancelSaleItem;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.DeleteSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSale;
using Ambev.DeveloperEvaluation.Application.Sales.GetSales;
using Ambev.DeveloperEvaluation.Application.Sales.UpdateSale;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Integration.Sales;

public class SaleHandlerIntegrationTests : IDisposable
{
    private readonly DefaultContext _dbContext;
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ISaleCacheService _cache;
    private readonly CreateSaleHandler _createHandler;
    private readonly GetSaleHandler _getHandler;
    private readonly GetSalesHandler _getSalesHandler;
    private readonly UpdateSaleHandler _updateHandler;
    private readonly CancelSaleHandler _cancelHandler;
    private readonly CancelSaleItemHandler _cancelItemHandler;
    private readonly DeleteSaleHandler _deleteHandler;

    public SaleHandlerIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseInMemoryDatabase($"IntegrationTests_{Guid.NewGuid()}")
            .Options;

        _dbContext = new DefaultContext(options);
        _saleRepository = new SaleRepository(_dbContext);
        _eventStore = Substitute.For<IEventStoreRepository>();
        _cache = Substitute.For<ISaleCacheService>();

        _cache.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((GetSaleResult?)null);

        _createHandler = new CreateSaleHandler(_saleRepository, _eventStore, NullLogger<CreateSaleHandler>.Instance);
        _getHandler = new GetSaleHandler(_saleRepository, _cache);
        _getSalesHandler = new GetSalesHandler(_saleRepository);
        _updateHandler = new UpdateSaleHandler(_saleRepository, _eventStore, _cache, NullLogger<UpdateSaleHandler>.Instance);
        _cancelHandler = new CancelSaleHandler(_saleRepository, _eventStore, _cache, NullLogger<CancelSaleHandler>.Instance);
        _cancelItemHandler = new CancelSaleItemHandler(_saleRepository, _eventStore, _cache, NullLogger<CancelSaleItemHandler>.Instance);
        _deleteHandler = new DeleteSaleHandler(_saleRepository, _cache, NullLogger<DeleteSaleHandler>.Instance);
    }

    [Fact(DisplayName = "CreateSale persists sale and publishes SaleCreated event")]
    public async Task CreateSale_ValidCommand_PersistsSaleAndPublishesEvent()
    {
        var command = BuildCreateCommand("INT-001");

        var result = await _createHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.SaleNumber.Should().Be("INT-001");

        var persisted = await _saleRepository.GetByIdAsync(result.Data.Id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.SaleNumber.Should().Be("INT-001");

        await _eventStore.Received(1).AppendAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CreateSale with 4 items applies 10% discount")]
    public async Task CreateSale_4Items_Applies10PercentDiscount()
    {
        var command = BuildCreateCommand("INT-DISC-10", quantity: 4, unitPrice: 100m);

        var result = await _createHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items[0].Discount.Should().Be(40m);
        result.Data.Items[0].TotalAmount.Should().Be(360m);
    }

    [Fact(DisplayName = "CreateSale with 10 items applies 20% discount")]
    public async Task CreateSale_10Items_Applies20PercentDiscount()
    {
        var command = BuildCreateCommand("INT-DISC-20", quantity: 10, unitPrice: 100m);

        var result = await _createHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items[0].Discount.Should().Be(200m);
        result.Data.Items[0].TotalAmount.Should().Be(800m);
    }

    [Fact(DisplayName = "CreateSale with more than 20 items returns business rule notification")]
    public async Task CreateSale_Over20Items_ReturnsBusinessRuleNotification()
    {
        var command = BuildCreateCommand("INT-OVER-20", quantity: 21);

        var result = await _createHandler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Notifications.Should().ContainSingle(n => n.Type == NotificationType.BusinessRule);
    }

    [Fact(DisplayName = "GetSale queries database when cache misses and then stores in cache")]
    public async Task GetSale_CacheMiss_QueriesDbAndStoresInCache()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommand("INT-GET-001"), CancellationToken.None);
        var saleId = createResult.Data!.Id;

        var getResult = await _getHandler.Handle(new GetSaleQuery(saleId), CancellationToken.None);

        getResult.IsSuccess.Should().BeTrue();
        getResult.Data!.SaleNumber.Should().Be("INT-GET-001");

        await _cache.Received(1).SetAsync(saleId, Arg.Any<GetSaleResult>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GetSale returns cached result without querying database")]
    public async Task GetSale_CacheHit_ReturnsCachedResultWithoutHittingDb()
    {
        var saleId = Guid.NewGuid();
        var cachedResult = new GetSaleResult { Id = saleId, SaleNumber = "CACHED-001" };

        _cache.GetAsync(saleId, Arg.Any<CancellationToken>()).Returns(cachedResult);

        var result = await _getHandler.Handle(new GetSaleQuery(saleId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.SaleNumber.Should().Be("CACHED-001");
    }

    [Fact(DisplayName = "GetSale with non-existent ID returns NotFound")]
    public async Task GetSale_NonExistentId_ReturnsNotFound()
    {
        var result = await _getHandler.Handle(new GetSaleQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Notifications.Should().ContainSingle(n => n.Type == NotificationType.NotFound);
    }

    [Fact(DisplayName = "UpdateSale persists changes, publishes event and invalidates cache")]
    public async Task UpdateSale_ValidCommand_PersistsChangesAndInvalidatesCache()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommand("INT-UPD-001"), CancellationToken.None);
        var saleId = createResult.Data!.Id;

        var updateCommand = BuildUpdateCommand(saleId, "INT-UPD-001-NEW");
        var updateResult = await _updateHandler.Handle(updateCommand, CancellationToken.None);

        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Data!.SaleNumber.Should().Be("INT-UPD-001-NEW");

        var persisted = await _saleRepository.GetByIdAsync(saleId, CancellationToken.None);
        persisted!.SaleNumber.Should().Be("INT-UPD-001-NEW");

        await _cache.Received(1).RemoveAsync(saleId, Arg.Any<CancellationToken>());
        await _eventStore.Received(2).AppendAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CancelSale marks sale as cancelled and publishes event")]
    public async Task CancelSale_ExistingSale_MarksAsCancelledAndPublishesEvent()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommand("INT-CAN-001"), CancellationToken.None);
        var saleId = createResult.Data!.Id;

        var cancelResult = await _cancelHandler.Handle(new CancelSaleCommand(saleId), CancellationToken.None);

        cancelResult.IsSuccess.Should().BeTrue();

        var persisted = await _saleRepository.GetByIdAsync(saleId, CancellationToken.None);
        persisted!.IsCancelled.Should().BeTrue();

        await _cache.Received(1).RemoveAsync(saleId, Arg.Any<CancellationToken>());
        await _eventStore.Received(2).AppendAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CancelSale on already cancelled sale returns Conflict")]
    public async Task CancelSale_AlreadyCancelled_ReturnsConflict()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommand("INT-CAN-DUP"), CancellationToken.None);
        var saleId = createResult.Data!.Id;

        await _cancelHandler.Handle(new CancelSaleCommand(saleId), CancellationToken.None);
        var result = await _cancelHandler.Handle(new CancelSaleCommand(saleId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Notifications.Should().ContainSingle(n => n.Type == NotificationType.Conflict);
    }

    [Fact(DisplayName = "CancelSaleItem marks specific item as cancelled")]
    public async Task CancelSaleItem_ValidItem_MarksItemAsCancelled()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommandWithTwoItems("INT-ITEM-CAN"), CancellationToken.None);
        var saleId = createResult.Data!.Id;
        var itemId = createResult.Data.Items[0].Id;

        var result = await _cancelItemHandler.Handle(new CancelSaleItemCommand(saleId, itemId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var sale = await _saleRepository.GetByIdAsync(saleId, CancellationToken.None);
        sale!.Items.First(i => i.Id == itemId).IsCancelled.Should().BeTrue();
        sale.Items.First(i => i.Id != itemId).IsCancelled.Should().BeFalse();
    }

    [Fact(DisplayName = "DeleteSale removes sale and returns null on subsequent query")]
    public async Task DeleteSale_ExistingSale_RemovesSale()
    {
        var createResult = await _createHandler.Handle(BuildCreateCommand("INT-DEL-001"), CancellationToken.None);
        var saleId = createResult.Data!.Id;

        var deleteResult = await _deleteHandler.Handle(new DeleteSaleCommand(saleId), CancellationToken.None);

        deleteResult.IsSuccess.Should().BeTrue();

        var persisted = await _saleRepository.GetByIdAsync(saleId, CancellationToken.None);
        persisted.Should().BeNull();
    }

    [Fact(DisplayName = "GetSales returns paginated list with correct total count")]
    public async Task GetSales_MultipleCreated_ReturnsPaginatedList()
    {
        await _createHandler.Handle(BuildCreateCommand("INT-LIST-001"), CancellationToken.None);
        await _createHandler.Handle(BuildCreateCommand("INT-LIST-002"), CancellationToken.None);
        await _createHandler.Handle(BuildCreateCommand("INT-LIST-003"), CancellationToken.None);

        var result = await _getSalesHandler.Handle(new GetSalesQuery(1, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Sales.Should().HaveCount(2);
        result.Data.Total.Should().BeGreaterThanOrEqualTo(3);
    }

    private static CreateSaleCommand BuildCreateCommand(
        string saleNumber,
        int quantity = 5,
        decimal unitPrice = 50m) => new()
    {
        SaleNumber = saleNumber,
        SaleDate = DateTime.UtcNow,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Integration Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Integration Branch",
        Items =
        [
            new CreateSaleItemCommand
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Integration Product",
                Quantity = quantity,
                UnitPrice = unitPrice
            }
        ]
    };

    private static CreateSaleCommand BuildCreateCommandWithTwoItems(string saleNumber) => new()
    {
        SaleNumber = saleNumber,
        SaleDate = DateTime.UtcNow,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Integration Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Integration Branch",
        Items =
        [
            new CreateSaleItemCommand { ProductId = Guid.NewGuid(), ProductName = "Item A", Quantity = 5, UnitPrice = 10m },
            new CreateSaleItemCommand { ProductId = Guid.NewGuid(), ProductName = "Item B", Quantity = 4, UnitPrice = 8m }
        ]
    };

    private static UpdateSaleCommand BuildUpdateCommand(Guid saleId, string saleNumber) => new()
    {
        Id = saleId,
        SaleNumber = saleNumber,
        SaleDate = DateTime.UtcNow,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Updated Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Updated Branch",
        Items =
        [
            new UpdateSaleItemCommand { ProductId = Guid.NewGuid(), ProductName = "Updated Product", Quantity = 5, UnitPrice = 25m }
        ]
    };

    public void Dispose() => _dbContext.Dispose();
}
