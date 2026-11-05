using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class LeaveRequestCreationTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public LeaveRequestCreationTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_ConRangoValido_CalculaDiasHabilesYQuedaPendienteDeGerente()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(2);

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), "Viaje familiar"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.BusinessDays);
        Assert.Equal("PendingManager", body.Status);
    }

    [Fact]
    public async Task Create_ConFechaFinAntesDeInicio_Regresa422()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(3);

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(-1), null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_ConFechaEnElPasado_Regresa422()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, DateTime.UtcNow.Date.AddDays(-5), DateTime.UtcNow.Date.AddDays(-3), null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_ConFechasSolapadas_Regresa409()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(4);

        var first = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), "Primera solicitud"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start.AddDays(1), start.AddDays(3), "Se solapa con la primera"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Create_ExcediendoSaldoDisponible_Regresa422()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(10);
        var end = start.AddDays(18); // 3 semanas completas = 15 dias habiles, saldo anual es 12

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, end, "Vacaciones largas"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_SinToken_Regresa401()
    {
        var client = _factory.CreateClient();
        var start = Dates.NextMonday(5);

        var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(1, start, start.AddDays(1), null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
