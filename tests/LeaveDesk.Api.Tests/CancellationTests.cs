using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class CancellationTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public CancellationTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cancel_SolicitudPendiente_PropioDueno_QuedaCancelada()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(30);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var cancelResponse = await employeeClient.PostAsync($"/api/leave-requests/{created!.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var afterCancel = await employeeClient.GetFromJsonAsync<LeaveRequestResponse>($"/api/leave-requests/{created.Id}");
        Assert.Equal("Cancelled", afterCancel!.Status);
    }

    [Fact]
    public async Task Cancel_SolicitudDeOtroEmpleado_Regresa403()
    {
        var ownerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var vacationId = await ownerClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(31);

        var created = await (await ownerClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var otherClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var response = await otherClient.PostAsync($"/api/leave-requests/{created!.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelApproved_AntesDeIniciar_RestauraSaldo()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var employeeId = await employeeClient.GetEmployeeIdAsync();
        var start = Dates.NextMonday(32);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var approved = await (await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve",
            new LeaveDecisionDto(created.RowVersion, null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal("Approved", approved!.Status);

        var balancesBefore = await employeeClient.GetFromJsonAsync<List<LeaveBalanceResponse>>($"/api/leave-balances/{employeeId}?year={start.Year}");
        Assert.Equal(3, balancesBefore!.Single(b => b.LeaveTypeCode == "VACATION").UsedDays);

        var cancelResponse = await employeeClient.PostAsJsonAsync(
            $"/api/leave-requests/{created.Id}/cancel-approved",
            new CancelApprovedLeaveDto(approved.RowVersion));

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<LeaveRequestResponse>();
        Assert.Equal("Cancelled", cancelled!.Status);

        var balancesAfter = await employeeClient.GetFromJsonAsync<List<LeaveBalanceResponse>>($"/api/leave-balances/{employeeId}?year={start.Year}");
        Assert.Equal(0, balancesAfter!.Single(b => b.LeaveTypeCode == "VACATION").UsedDays);
    }
}
