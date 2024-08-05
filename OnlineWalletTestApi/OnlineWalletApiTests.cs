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
        var data = new Dictionary<string, double>
        {
            { "amount", 3 }
        };
        
        var depositResponse = await Request.PostAsync("/onlinewallet/deposit", new() { DataObject = data });
        await Assertions.Expect(depositResponse).ToBeOKAsync();

        JsonElement? res = await depositResponse.JsonAsync();
        var amountValue = res?.GetProperty("amount");
        Assert.AreEqual(3, amountValue?.GetDouble()!);
        Assert.AreEqual(3, await GetAmountValue());
    }
    
    [TestMethod]
    public async Task Test_WalletDeposit_String()
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
        Assert.AreEqual(3, await GetAmountValue());
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
        // Assert.AreEqual("The JSON value could not be converted to System.Decimal", amount?.First().GetString());
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

    private async Task ResetApiState()
    {
        var currentBalance = await GetAmountValue();
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

    private async Task<IAPIResponse> WithdrawWalletAmount(double currentBalance)
    {
        var data = new Dictionary<string, double>
        {
            { "amount", currentBalance }
        };
        return await Request.PostAsync("/onlinewallet/withdraw", new() { DataObject = data });
    }

    private async Task<double> GetAmountValue()
    {
        var response = await GetWalletBalance();

        JsonElement? jsonBody = await response.JsonAsync();
        var amountValue = jsonBody?.GetProperty("amount");
        return amountValue?.GetDouble() ?? throw new Exception("Get response amount should not be null");
    }
}