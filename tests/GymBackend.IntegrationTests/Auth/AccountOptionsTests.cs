using GymBackend.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GymBackend.IntegrationTests.Auth;

public sealed class AccountOptionsTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("Email:Mode", "Unknown")]
    [InlineData("Accounts:ActivationHours", "0")]
    [InlineData("Accounts:ResetMinutes", "-1")]
    [InlineData("Accounts:FrontendBaseUrl", "relative")]
    [InlineData("Accounts:FrontendBaseUrl", "ftp://frontend.example")]
    [InlineData("Accounts:FrontendBaseUrl", "https://frontend.example/?query=value")]
    [InlineData("Reservations:CategoryMaxDaysAhead:Estudiante", "-1")]
    [InlineData("Reservations:CategoryMaxDaysAhead:Unknown", "2")]
    public void InvalidOption_IsRejectedAtStartup(string key, string value)
    {
        using var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value, ["Database:Initialize"] = "false" })));
        if (key == "Email:Mode") Assert.Throws<InvalidOperationException>(() => host.CreateClient());
        else Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    [Theory]
    [InlineData("Smtp")]
    [InlineData("Pickup")]
    public void EnabledEmail_RequiresFrontendUrl(string mode)
    {
        using var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Initialize"] = "false",
                ["Email:Mode"] = mode,
                ["Email:Host"] = "smtp.example.test",
                ["Email:PickupDirectory"] = Path.GetTempPath(),
                ["Accounts:FrontendBaseUrl"] = "",
            })));
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    [Theory]
    [InlineData("Email:Mode", "Pickup", "https://frontend.example")]
    [InlineData("Email:Mode", "Disabled", "http://frontend.example")]
    public void Production_RejectsPickupAndHttp(string key, string value, string frontend)
    {
        using var host = factory.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Initialize"] = "false",
                [key] = value,
                ["Email:PickupDirectory"] = Path.GetTempPath(),
                ["Accounts:FrontendBaseUrl"] = frontend,
            }));
        });
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }
}
