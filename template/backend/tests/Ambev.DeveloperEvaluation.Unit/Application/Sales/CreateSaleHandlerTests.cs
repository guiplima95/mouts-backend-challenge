using Ambev.DeveloperEvaluation.Application.Sales.CreateSale;
using Ambev.DeveloperEvaluation.Application.Sales.Common;
using Ambev.DeveloperEvaluation.Domain.Entities;
using Ambev.DeveloperEvaluation.Domain.Events;
using Ambev.DeveloperEvaluation.Domain.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Ambev.DeveloperEvaluation.Unit.Application.Sales;

public class CreateSaleHandlerTests
{
    private readonly ISaleRepository _saleRepository;
    private readonly IEventStoreRepository _eventStore;
    private readonly ILogger<CreateSaleHandler> _logger;
    private readonly CreateSaleHandler _handler;

    public CreateSaleHandlerTests()
    {
        _saleRepository = Substitute.For<ISaleRepository>();
        _eventStore = Substitute.For<IEventStoreRepository>();
        _logger = Substitute.For<ILogger<CreateSaleHandler>>();
        _handler = new CreateSaleHandler(_saleRepository, _eventStore, _logger);
    }

    private static CreateSaleCommand ValidCommand() => new()
    {
        SaleNumber = "SALE-001",
        SaleDate = DateTime.UtcNow,
        CustomerId = Guid.NewGuid(),
        CustomerName = "Test Customer",
        BranchId = Guid.NewGuid(),
        BranchName = "Test Branch",
        Items =
        [
            new CreateSaleItemCommand
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Product A",
                Quantity = 5,
                UnitPrice = 100m
            }
        ]
    };

    [Fact(DisplayName = "Given valid sale When creating Then returns success result")]
    public async Task Handle_ValidCommand_ReturnsSuccess()
    {
        var command = ValidCommand();
        _saleRepository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Sale>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.SaleNumber.Should().Be("SALE-001");
        await _saleRepository.Received(1).CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>());
        await _eventStore.Received(1).AppendAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Given item with 4 items When creating Then 10% discount applied")]
    public async Task Handle_4Items_Applies10PercentDiscount()
    {
        var command = ValidCommand();
        command.Items[0].Quantity = 4;
        command.Items[0].UnitPrice = 100m;

        _saleRepository.CreateAsync(Arg.Any<Sale>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Sale>());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items[0].Discount.Should().Be(40m);
        result.Data.Items[0].TotalAmount.Should().Be(360m);
    }

    [Fact(DisplayName = "Given empty command When creating Then returns validation notifications")]
    public async Task Handle_EmptyCommand_ReturnsValidationNotifications()
    {
        var command = new CreateSaleCommand();
        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Notifications.Should().NotBeEmpty();
        result.Notifications.Should().OnlyContain(n => n.Type == NotificationType.Validation);
    }

    [Fact(DisplayName = "Given item with more than 20 quantity When creating Then returns business rule notification")]
    public async Task Handle_Over20Quantity_ReturnsBusinessRuleNotification()
    {
        var command = ValidCommand();
        command.Items[0].Quantity = 21;

        var result = await _handler.Handle(command, CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Notifications.Should().ContainSingle(n => n.Type == NotificationType.BusinessRule);
        result.Notifications.First().Message.Should().Contain("20");
    }
}
