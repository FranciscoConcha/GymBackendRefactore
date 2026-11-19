using System.Net;
using System.Net.Http.Json;
using GymBackend.IntegrationTests.Infrastructure;
using static GymBackend.IntegrationTests.Infrastructure.Api;

namespace GymBackend.IntegrationTests.Auth;

public sealed class InputValidationTests(GymApiFactory factory)
{
    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("Juan\" onmouseover=\"x")]
    public async Task Register_UnsafeName_BadRequest(string name)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = name,
            lastName = "Test",
            email = $"{rut}@example.test",
            password = "Password123!",
            role = "Usuario",
            category = "Estudiante",
        }, JsonOptions, Ct), HttpStatusCode.BadRequest, "El nombre contiene caracteres no permitidos o supera los 80 caracteres.");
    }

    [Theory]
    [InlineData("José Núñez")]
    [InlineData("María-José O'Higgins")]
    public async Task Register_SafeName_Ok(string name)
    {
        using var admin = await factory.CreateAdminClientAsync();
        var rut = TestData.NextRut();
        var json = await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut,
            firstName = name,
            lastName = "Test",
            email = $"{rut}@example.test",
            password = "Password123!",
            role = "Usuario",
            category = "Estudiante",
        }, JsonOptions, Ct), HttpStatusCode.OK, "Usuario registrado correctamente.");
        Assert.Equal(name, json.GetProperty("user").GetProperty("firstName").GetString());
    }

    [Theory]
    [InlineData("a<b>@x.cl")]
    [InlineData("no-es-correo")]
    public async Task Register_InvalidEmail_BadRequest(string email)
    {
        using var admin = await factory.CreateAdminClientAsync();
        await Expect(await admin.PostAsJsonAsync("/api/v1/auth/register", new
        {
            rut = TestData.NextRut(),
            firstName = "Juan",
            lastName = "Test",
            email,
            password = "Password123!",
            role = "Usuario",
            category = "Estudiante",
        }, JsonOptions, Ct), HttpStatusCode.BadRequest, "El correo no tiene un formato válido.");
    }
}
