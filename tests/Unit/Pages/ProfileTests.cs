using System.Net;
using Desk.Data;
using Desk.Models;
using Desk.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Desk.Tests.Pages;

public class ProfileTests
{
    private static Mock<SignInManager<DeskUser>> CreateSignInManagerMock(Mock<UserManager<DeskUser>> userManagerMock)
    {
        return new Mock<SignInManager<DeskUser>>(
            userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<DeskUser>>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<DeskUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<DeskUser>>().Object);
    }

    private static readonly ApiKeyProtector Protector = new(
        DataProtectionProvider.Create("Desk.Tests"));

    private static (Desk.Areas.Identity.Pages.Account.Manage.IndexModel model,
        MockHttpMessageHandler handler,
        Mock<UserManager<DeskUser>> userManagerMock) CreateModel()
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com" };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);
        var apiClient = new ApiClient(config, sessionManager, httpClient);
        var apiManager = new ApiManager(apiClient);

        var store = new Mock<IUserStore<DeskUser>>();
        var userManagerMock = new Mock<UserManager<DeskUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInManagerMock = CreateSignInManagerMock(userManagerMock);

        var model = new Desk.Areas.Identity.Pages.Account.Manage.IndexModel(
            userManagerMock.Object, signInManagerMock.Object, apiManager, sessionManager, config,
            Protector,
            NullLogger<Desk.Areas.Identity.Pages.Account.Manage.IndexModel>.Instance);

        return (model, handler, userManagerMock);
    }

    [Fact]
    public async Task SaveApiKey_ValidatesWithStatusEndpoint_BeforeSaving()
    {
        var (model, handler, userManagerMock) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"operation_left": 100, "signature_left": 50}""");

        var user = new DeskUser { Id = "1", Email = "test@test.com" };
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<DeskUser>()))
            .ReturnsAsync(IdentityResult.Success);

        model.ApiKeyInput = "itk_live_valid_key";
        _ = await model.OnPostSaveApiKeyAsync();

        // Should have called the status endpoint for validation
        Assert.Contains("status", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SaveApiKey_SavesKey_WhenValidationSucceeds()
    {
        var (model, handler, userManagerMock) = CreateModel();
        handler.WithResponse(HttpStatusCode.OK,
            """{"operation_left": 100, "signature_left": 50}""");

        var user = new DeskUser { Id = "1", Email = "test@test.com" };
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<DeskUser>()))
            .ReturnsAsync(IdentityResult.Success);

        model.ApiKeyInput = "itk_live_valid_key";
        _ = await model.OnPostSaveApiKeyAsync();

        userManagerMock.Verify(m => m.UpdateAsync(
            It.Is<DeskUser>(u => u.ApiKey != null && u.ApiKey.StartsWith("ENC:"))), Times.Once);
    }

    [Fact]
    public async Task SaveApiKey_ReturnsError_WhenValidationFails()
    {
        var (model, handler, userManagerMock) = CreateModel();
        handler.WithResponse(HttpStatusCode.Unauthorized,
            """{"problem_details":{"title":"Unauthorized","detail":"Invalid API key"}}""");

        var user = new DeskUser { Id = "1", Email = "test@test.com" };
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        model.ApiKeyInput = "itk_live_bad_key";
        _ = await model.OnPostSaveApiKeyAsync();

        Assert.NotNull(model.ErrorMessage);
        userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<DeskUser>()), Times.Never);
    }

    [Fact]
    public async Task SaveApiKey_ReturnsError_WhenApiUnreachable()
    {
        var config = new DeskConfig { ApiUrl = "https://api.unreachable.com" };
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);

        // Create a client that will throw on connection
        var badHandler = new UnreachableHttpMessageHandler();
        var badHttpClient = new HttpClient(badHandler) { BaseAddress = new Uri("https://api.unreachable.com/") };
        var apiClient = new ApiClient(config, sessionManager, badHttpClient);
        var apiManager = new ApiManager(apiClient);

        var store = new Mock<IUserStore<DeskUser>>();
        var userManagerMock = new Mock<UserManager<DeskUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInManagerMock = CreateSignInManagerMock(userManagerMock);

        var user = new DeskUser { Id = "1", Email = "test@test.com" };
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);

        var model = new Desk.Areas.Identity.Pages.Account.Manage.IndexModel(
            userManagerMock.Object, signInManagerMock.Object, apiManager, sessionManager, config,
            Protector,
            NullLogger<Desk.Areas.Identity.Pages.Account.Manage.IndexModel>.Instance);

        model.ApiKeyInput = "itk_live_key";
        _ = await model.OnPostSaveApiKeyAsync();

        Assert.NotNull(model.ErrorMessage);
        userManagerMock.Verify(m => m.UpdateAsync(It.IsAny<DeskUser>()), Times.Never);
    }

    [Fact]
    public async Task SaveApiKey_UpdatesSession_AfterSave()
    {
        var config = new DeskConfig { ApiUrl = "https://api.test.com" };
        var handler = new MockHttpMessageHandler()
            .WithResponse(HttpStatusCode.OK,
                """{"operation_left": 100, "signature_left": 50}""");
        var httpClient = new HttpClient(handler);
        var httpContext = new DefaultHttpContext { Session = new TestSession() };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var sessionManager = new SessionManager(accessor, config);
        var apiClient = new ApiClient(config, sessionManager, httpClient);
        var apiManager = new ApiManager(apiClient);

        var store = new Mock<IUserStore<DeskUser>>();
        var userManagerMock = new Mock<UserManager<DeskUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        var signInManagerMock = CreateSignInManagerMock(userManagerMock);

        var user = new DeskUser { Id = "1", Email = "test@test.com" };
        userManagerMock.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(user);
        userManagerMock.Setup(m => m.UpdateAsync(It.IsAny<DeskUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var model = new Desk.Areas.Identity.Pages.Account.Manage.IndexModel(
            userManagerMock.Object, signInManagerMock.Object, apiManager, sessionManager, config,
            Protector,
            NullLogger<Desk.Areas.Identity.Pages.Account.Manage.IndexModel>.Instance);

        model.ApiKeyInput = "itk_live_new_key";
        _ = await model.OnPostSaveApiKeyAsync();

        // Session should now have the new API key
        Assert.Equal("itk_live_new_key", sessionManager.GetApiKey());
    }
}

/// <summary>Helper that simulates unreachable API</summary>
public class UnreachableHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Connection refused");
    }
}
