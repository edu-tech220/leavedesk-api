using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class AuthTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public AuthTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ConCredencialesValidas_RegresaToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(TestUsers.EmployeeOne, TestUsers.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Employee", body.Role);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_Regresa401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(TestUsers.EmployeeOne, "contrasena-incorrecta"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ConCorreoInexistente_Regresa401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("no-existe@leavedesk.test", TestUsers.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EndpointProtegido_SinToken_Regresa401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/employees/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
