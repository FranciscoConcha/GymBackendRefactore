using System.Net;
using System.Text.Json.Nodes;
using GymBackend.Application.Email;
using GymBackend.Extensions;
using GymBackend.Models;
using GymBackend.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GymBackend.UnitTests.Email;

public sealed class BrevoApiEmailSenderTests
{
    private const string ApiKey = "private-test-api-key";
    private const string ResponseBody = "private-provider-response";

    [Theory]
    [InlineData("UCN Gym")]
    [InlineData("")]
    public async Task Created_SendsExactRequest(string name)
    {
        var settings = new EmailOptions { ApiKey = ApiKey, From = "sender@example.test", FromName = name };
        var handler = new FakeHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://brevo.example.test/v3/smtp/email", request.RequestUri!.AbsoluteUri);
            Assert.Equal(ApiKey, Assert.Single(request.Headers.GetValues("api-key")));
            Assert.Equal("application/json", Assert.Single(request.Headers.Accept).MediaType);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            var actual = JsonNode.Parse(await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var sender = new JsonObject { ["email"] = settings.From };
            if (name.Length > 0) sender["name"] = name;
            var expected = new JsonObject
            {
                ["sender"] = sender,
                ["to"] = new JsonArray(new JsonObject { ["email"] = "recipient@example.test" }),
                ["subject"] = "Activación",
                ["textContent"] = "Contenido privado",
            };
            Assert.True(JsonNode.DeepEquals(expected, actual));
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"messageId\":\"test\"}") };
        });
        using var provider = BuildProvider(handler);
        await new BrevoApiEmailSender(provider.GetRequiredService<IHttpClientFactory>(), Microsoft.Extensions.Options.Options.Create(settings))
            .SendAsync(Mail(), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(500)]
    public async Task UnexpectedStatus_ThrowsSanitizedException(int status)
    {
        var handler = new FakeHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent(ResponseBody + ApiKey),
        }));
        using var provider = BuildProvider(handler);
        var sender = new BrevoApiEmailSender(provider.GetRequiredService<IHttpClientFactory>(),
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { ApiKey = ApiKey }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Mail(), TestContext.Current.CancellationToken));
        Assert.Equal("No se pudo enviar el correo.", error.Message);
        Assert.DoesNotContain(ApiKey, error.ToString());
        Assert.DoesNotContain(ResponseBody, error.ToString());
        Assert.Null(error.InnerException);
    }

    [Fact]
    public async Task TransportException_IsSanitized()
    {
        using var provider = BuildProvider(new FakeHandler(_ => throw new HttpRequestException(ApiKey + ResponseBody)));
        var sender = new BrevoApiEmailSender(provider.GetRequiredService<IHttpClientFactory>(), Microsoft.Extensions.Options.Options.Create(new EmailOptions { ApiKey = ApiKey }));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(Mail(), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(ApiKey, error.ToString());
        Assert.DoesNotContain(ResponseBody, error.ToString());
    }

    [Theory]
    [InlineData("", "https://api.brevo.com/", "Configure Email:ApiKey para BrevoApi.")]
    [InlineData(ApiKey, "http://api.example.test/", "Configure Email:ApiBaseUrl")]
    public async Task InvalidOptions_FailAtStartup(string key, string url, string expected)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:Mode"] = "BrevoApi",
            ["Email:ApiKey"] = key,
            ["Email:ApiBaseUrl"] = url,
            ["Accounts:FrontendBaseUrl"] = "https://frontend.example.test",
            ["Jwt:Key"] = new string('a', 32),
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Qr:SigningKey"] = new string('b', 32),
        });
        builder.Services.AddGymOptions();
        using var host = builder.Build();
        var error = await Assert.ThrowsAnyAsync<Exception>(() => host.StartAsync(TestContext.Current.CancellationToken));
        Assert.Contains(expected, error.ToString());
    }

    private static EmailOutbox Mail() => new()
    {
        To = "recipient@example.test",
        Subject = "Activación",
        Body = "Contenido privado",
    };

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("brevo", client => client.BaseAddress = new Uri("https://brevo.example.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}

