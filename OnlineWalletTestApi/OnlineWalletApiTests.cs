using System.Text.Json;
using Microsoft.Playwright;

namespace OnlineWalletTestApi;

[TestClass]
public class OnlineWalletApiTests : PlaywrightTest
{
    private IAPIRequestContext Request = null!;

    [TestMethod]
    public async Task Test_GetBalance_OkStatus()
    {
        
        var response = await Request.GetAsync("/onlinewallet/balance");
        await Expect(response).ToBeOKAsync();
        
        JsonElement? jsonBody = await response.JsonAsync();
        var amountValue = jsonBody?.GetProperty("amount");
        Assert.AreEqual(0, amountValue?.GetInt32()!);
    }

    [TestInitialize]
    public async Task SetUpApiTesting()
    {
        await CreateApiRequestContext();
    }

    private async Task CreateApiRequestContext()
    {
        Request = await Playwright.APIRequest.NewContextAsync(new()
        {
            // All requests we send go to this API endpoint.
            BaseURL = "http://localhost:5047/swagger/"
        });
    }

    [TestCleanup]
    public async Task TearDownApiTesting()
    {
        await Request.DisposeAsync();
    }
}