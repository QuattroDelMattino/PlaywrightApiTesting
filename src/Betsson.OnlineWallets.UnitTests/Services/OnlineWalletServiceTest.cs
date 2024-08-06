using System.Threading.Tasks;
using Betsson.OnlineWallets.Data.Models;
using Betsson.OnlineWallets.Data.Repositories;
using Betsson.OnlineWallets.Exceptions;
using Betsson.OnlineWallets.Models;
using Betsson.OnlineWallets.Services;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NUnit.Framework;

namespace Betsson.OnlineWallets.UnitTests.Services;

[TestClass]
[TestSubject(typeof(OnlineWalletService))]
public class OnlineWalletServiceTest
{
    private Mock<IOnlineWalletRepository> externalServiceClientMock;
    private OnlineWalletService onlineWalletService;

    [SetUp]
    public void MyServiceSetup()
    {
        externalServiceClientMock = new Mock<IOnlineWalletRepository>();
        onlineWalletService = new OnlineWalletService(externalServiceClientMock.Object);
    }

    [Test]
    public async Task TestGetBalanceWithZeroBalanceBefore()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync(new OnlineWalletEntry { Amount = 3 });
        // Act
        var result = await onlineWalletService.GetBalanceAsync();
        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(3));
    }

    [Test]
    public async Task TestGetBalanceWithTransactionsThenReturnSum()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync(new OnlineWalletEntry
            {
                Amount = 3,
                BalanceBefore = 2
            });
        // Act
        var result = await onlineWalletService.GetBalanceAsync();
        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(5));
    }

    [Test]
    public async Task TestGetBalanceWithNullWalletEntryThenReturnZero()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync((OnlineWalletEntry)null);
        // Act
        var result = await onlineWalletService.GetBalanceAsync();
        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(0));
    }

    [Test]
    public async Task TestDepositFundsWithNullWalletEntryThenReturnDepositedAmount()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync((OnlineWalletEntry)null);
        externalServiceClientMock.Setup(esc => esc.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await onlineWalletService.DepositFundsAsync(new Deposit { Amount = 5 });

        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(5));
        externalServiceClientMock.Verify(proxy => proxy.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()),
            Times.Once());
    }

    [Test]
    public async Task TestDepositFundsWithNegativeWalletEntryThenReturnSum()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync(new OnlineWalletEntry { Amount = -3 });
        externalServiceClientMock.Setup(esc => esc.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await onlineWalletService.DepositFundsAsync(new Deposit { Amount = 5 });

        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(2));
        externalServiceClientMock.Verify(proxy => proxy.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()),
            Times.Once());
    }

    [Test]
    public async Task TestWithdrawFundsWithNotEnoughBalanceThenThrowException()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync(new OnlineWalletEntry { Amount = 3 });
        externalServiceClientMock.Setup(esc => esc.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
            .Returns(Task.CompletedTask);

        // Assert
        NUnit.Framework.Assert.ThrowsAsync<InsufficientBalanceException>(() => onlineWalletService.WithdrawFundsAsync(
            new Withdrawal { Amount = 5 })
        );
    }

    [Test]
    public async Task TestWithdrawFundsWithAvailableBalanceThenWithdrawSuccess()
    {
        // Arrange
        externalServiceClientMock.Setup(esc => esc.GetLastOnlineWalletEntryAsync())
            .ReturnsAsync(new OnlineWalletEntry { Amount = 6 });
        externalServiceClientMock.Setup(esc => esc.InsertOnlineWalletEntryAsync(It.IsAny<OnlineWalletEntry>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await onlineWalletService.WithdrawFundsAsync(new Withdrawal { Amount = 5 });
        // Assert
        NUnit.Framework.Assert.That(result.Amount, Is.EqualTo(1));
    }
}