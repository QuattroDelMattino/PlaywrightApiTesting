using System.Text.Json;
using Microsoft.Playwright;

namespace OnlineWalletTestApi;

[TestClass]
public class OnlineWalletApiTests : PlaywrightTest
{
    private IAPIRequestContext Request = null!;
    private static string? BASE_URL = "http://localhost:5047/swagger/";

    [TestInitialize]
    public async Task SetUpApiTesting()
    {
        await CreateApiRequestContext();
        await ResetApiState();
    }

    [TestCleanup]
    public async Task TearDownApiTesting()
    {
        await Request.DisposeAsync();
    }

    [TestMethod]
    public async Task Test_GetBalance_OkStatus()
    {
        var response = await GetWalletBalance();
        await CheckBalanceIsZero(response);
    }

    [TestMethod]
    public async Task Test_GetBalance_IncorrectEndpoint()
    {
        var response = await Request.GetAsync("/onlinewallet/balancee");
        Assert.AreEqual(404, response.Status);
        Assert.AreEqual("Not Found", response.StatusText);
    } 
    
    [TestMethod]
    public async Task Test_WalletDeposit_OkStatus()
    {
        var depositResponse = await DepositWalletAmount(3);
        await Assertions.Expect(depositResponse).ToBeOKAsync();

        JsonElement? res = await depositResponse.JsonAsync();
        var amountValue = res?.GetProperty("amount");
        Assert.AreEqual(3, amountValue?.GetDouble()!);
        Assert.AreEqual(3, await GetBalance());
    }
    
    [TestMethod]
    public async Task Test_WalletDeposit_String_OkStatus()
    {
        var data = new Dictionary<string, string>
        {
            { "amount", "3" }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
        await Assertions.Expect(depositResponse).ToBeOKAsync();

        JsonElement? res = await depositResponse.JsonAsync();
        var amountValue = res?.GetProperty("amount");
        Assert.AreEqual(3, amountValue?.GetDouble()!);
        Assert.AreEqual(3, await GetBalance());
    }
    
    [TestMethod]
    public async Task Test_WalletDeposit_InvalidAmount_ExpectBadRequest()
    {
        var data = new Dictionary<string, string>
        {
            { "amount", "99999999999999999999999999999" }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
        Assert.AreEqual(400, depositResponse.Status);
        Assert.AreEqual("Bad Request", depositResponse.StatusText);
        JsonElement? jsonBody = await depositResponse.JsonAsync();
        var errors = jsonBody?.GetProperty("errors");
        var depositRequest = errors?.GetProperty("depositRequest").EnumerateArray();
        Assert.AreEqual("The depositRequest field is required.", depositRequest?.First().GetString());
        var amount = errors?.GetProperty("$.amount").EnumerateArray();
        Assert.IsTrue(amount?.First().GetString().Contains("The JSON value could not be converted to System.Decimal"));
    }
    
    [TestMethod]
    public async Task Test_WalletDeposit_NegativeAmount_ExpectBadRequest()
    {
        var data = new Dictionary<string, string>
        {
            { "amount", "-5" }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
        Assert.AreEqual(400, depositResponse.Status);
        Assert.AreEqual("Bad Request", depositResponse.StatusText);
        JsonElement? jsonBody = await depositResponse.JsonAsync();
        var errors = jsonBody?.GetProperty("errors").GetProperty("Amount").EnumerateArray();
        Assert.AreEqual("'Amount' must be greater than or equal to '0'.", errors?.First().GetString());
    }
    
    /*
     * Expected here would be to fail
     */
    [TestMethod]
    public async Task Test_WalletDeposit_EmptyPayload_ExpectBalanceNotChanged()
    {
        var data = new Dictionary<string, string>();
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
        await CheckBalanceIsZero(depositResponse);
    }

    [TestMethod]
    public async Task Test_WalletDeposit_NoPayload_ExpectUnsupportedMediaType()
    {
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = null });
        Assert.AreEqual(415, depositResponse.Status);
        Assert.AreEqual("Unsupported Media Type", depositResponse.StatusText);
    }
    
    [TestMethod]
    public async Task Test_WalletWithdrawal_OkStatus()
    {
        var data = new Dictionary<string, double>
        {
            { "amount", 0 }
        };
        var withdrawResponse = await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });

        await Assertions.Expect(withdrawResponse).ToBeOKAsync();

        JsonElement? res = await withdrawResponse.JsonAsync();
        var amountValue = res?.GetProperty("amount");
        Assert.AreEqual(0, amountValue?.GetDouble()!);
        Assert.AreEqual(0, await GetBalance());
    }
    
    [TestMethod]
    public async Task Test_WalletWithdrawal_String_OkStatus()
    {
        await DepositWalletAmount(3);
        var data = new Dictionary<string, string>
        {
            { "amount", "3" }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });
        await Assertions.Expect(depositResponse).ToBeOKAsync();

        JsonElement? res = await depositResponse.JsonAsync();
        var amountValue = res?.GetProperty("amount");
        Assert.AreEqual(0, amountValue?.GetDouble()!);
        Assert.AreEqual(0, await GetBalance());
    }
    
    [TestMethod]
    public async Task Test_WalletWithdrawal_InsufficientFunds_ExpectBadRequest()
    {
        await DepositWalletAmount(33);
        var withdrawResponse = await WithdrawWalletAmount(55);

        Assert.AreEqual(400, withdrawResponse.Status);
        Assert.AreEqual("Bad Request", withdrawResponse.StatusText); 
        
        JsonElement? jsonBody = await withdrawResponse.JsonAsync();
        Assert.AreEqual("InsufficientBalanceException", jsonBody?.GetProperty("type").GetString());
        Assert.AreEqual("Invalid withdrawal amount. There are insufficient funds.", jsonBody?.GetProperty("title").GetString());
        Assert.IsNotNull( jsonBody?.GetProperty("detail"));
    }
    
    [TestMethod]
    public async Task Test_WalletWithdrawal_NegativeAmount_ExpectBadRequest()
    {
        var data = new Dictionary<string, string>
        {
            { "amount", "-5" }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });
        Assert.AreEqual(400, depositResponse.Status);
        Assert.AreEqual("Bad Request", depositResponse.StatusText);
        JsonElement? jsonBody = await depositResponse.JsonAsync();
        var errors = jsonBody?.GetProperty("errors").GetProperty("Amount").EnumerateArray();
        Assert.AreEqual("'Amount' must be greater than or equal to '0'.", errors?.First().GetString());
    }

    [TestMethod]
    public async Task Test_WalletWithdrawal_EmptyPayload_ExpectBalanceNotChanged()
    {
        var data = new Dictionary<string, string>();
        var depositResponse = await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });
        await CheckBalanceIsZero(depositResponse);
    }

    [TestMethod]
    public async Task Test_WalletWithdrawal_NoPayload_ExpectUnsupportedMediaType()
    {
        var depositResponse = await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = null });
        Assert.AreEqual(415, depositResponse.Status);
        Assert.AreEqual("Unsupported Media Type", depositResponse.StatusText);
    }
    
    private async Task ResetApiState()
    {
        var currentBalance = await GetBalance();
        if (currentBalance > 0)
        {
            var response = await WithdrawWalletAmount(currentBalance);
            await CheckBalanceIsZero(response);
        }
    }

    private async Task CheckBalanceIsZero(IAPIResponse response)
    {
        await Expect(response).ToBeOKAsync();

        JsonElement? jsonBody = await response.JsonAsync();
        var amountValue = jsonBody?.GetProperty("amount");
        Assert.AreEqual(0, amountValue?.GetDouble()!);
    }

    private async Task CreateApiRequestContext()
    {
        Request = await Playwright.APIRequest.NewContextAsync(new()
        {
            // All requests we send go to this API endpoint.
            BaseURL = BASE_URL
        });
    }

    private async Task<IAPIResponse> GetWalletBalance()
    {
        var response = await Request.GetAsync("/onlinewallet/balance");
        return response;
    }
    
    private async Task<IAPIResponse> DepositWalletAmount(double amount)
    {
        var data = new Dictionary<string, double>
        {
            { "amount", amount }
        };
        
        return await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
    }

    private async Task<IAPIResponse> WithdrawWalletAmount(double currentBalance)
    {
        var data = new Dictionary<string, double>
        {
            { "amount", currentBalance }
        };
        return await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });
    }

    private async Task<double> GetBalance()
    {
        var response = await GetWalletBalance();

        JsonElement? jsonBody = await response.JsonAsync();
        var amountValue = jsonBody?.GetProperty("amount");
        return amountValue?.GetDouble() ?? throw new Exception("Get response amount should not be null");
    }
}