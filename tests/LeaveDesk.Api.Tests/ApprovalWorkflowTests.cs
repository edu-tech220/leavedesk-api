using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class ApprovalWorkflowTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public ApprovalWorkflowTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManagerAprueba_PermisoCorto_QuedaAprobadoDirectoYActualizaSaldo()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var employeeId = await employeeClient.GetEmployeeIdAsync();
        var start = Dates.NextMonday(20);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), "Corto"))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var approveResponse = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto(created.RowVersion, "Adelante"));

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        var approved = await approveResponse.Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal("Approved", approved!.Status);

        var balances = await managerClient.GetFromJsonAsync<List<LeaveBalanceResponse>>($"/api/leave-balances/{employeeId}?year={start.Year}");
        var vacationBalance = balances!.Single(b => b.LeaveTypeCode == "VACATION");
        Assert.Equal(3, vacationBalance.UsedDays);
    }

    [Fact]
    public async Task ManagerYLuegoHr_PermisoLargo_RequiereAmbasAprobaciones()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var employeeId = await employeeClient.GetEmployeeIdAsync();
        var start = Dates.NextMonday(21);
        var end = start.AddDays(11); // dos semanas completas = 10 dias habiles > umbral de 5

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, end, "Largo"))).Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal(10, created!.BusinessDays);

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var afterManager = await (await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created.Id}/manager-decision/approve",
            new LeaveDecisionDto(created.RowVersion, null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        Assert.Equal("PendingHr", afterManager!.Status);

        var hrClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Hr);
        var afterHr = await hrClient.PutAsJsonAsync(
            $"/api/leave-requests/{created.Id}/hr-decision/approve",
            new LeaveDecisionDto(afterManager.RowVersion, "Aprobado por RH"));

        Assert.Equal(HttpStatusCode.OK, afterHr.StatusCode);
        var finalState = await afterHr.Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal("Approved", finalState!.Status);

        var balances = await hrClient.GetFromJsonAsync<List<LeaveBalanceResponse>>($"/api/leave-balances/{employeeId}?year={start.Year}");
        Assert.Equal(10, balances!.Single(b => b.LeaveTypeCode == "VACATION").UsedDays);
    }

    [Fact]
    public async Task GerenteQueNoEsDirecto_NoPuedeAprobar_Regresa403()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(22);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var otherManagerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.OtherManager);
        var response = await otherManagerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto(created.RowVersion, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Empleado_NoPuedeLlamarEndpointDeAprobacion_Regresa403()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(23);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var response = await employeeClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto(created.RowVersion, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagerRechaza_QuedaRechazadoYBloqueaDecisionesPosteriores()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(24);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var rejectResponse = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/reject",
            new LeaveRejectionDto(created.RowVersion, "No hay cobertura"));

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);
        var rejected = await rejectResponse.Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal("Rejected", rejected!.Status);

        var secondAttempt = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created.Id}/manager-decision/approve",
            new LeaveDecisionDto(rejected.RowVersion, null));

        Assert.Equal(HttpStatusCode.Conflict, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task Decision_ConRowVersionDesactualizado_Regresa409()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(25);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var staleRowVersion = Convert.ToBase64String(new byte[16]);

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var response = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto(staleRowVersion, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Decision_ConRowVersionMalFormado_Regresa422()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(26);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var response = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto("no-es-base64!!", null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
