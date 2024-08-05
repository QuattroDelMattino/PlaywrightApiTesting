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
        return amountValue?.GetDouble() ?? throw new Exception("Get response amount cannot be null");
    }
}