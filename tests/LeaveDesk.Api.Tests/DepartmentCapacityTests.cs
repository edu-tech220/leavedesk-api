using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

/// <summary>
/// El departamento de Ingenieria se siembra con MaxConcurrentLeaves = 2: a lo sumo dos
/// empleados pueden tener un permiso aprobado con fechas solapadas al mismo tiempo.
/// </summary>
public class DepartmentCapacityTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public DepartmentCapacityTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TercerPermisoSolapado_SuperaCupoDelDepartamento_Regresa422YNoAlteraEstado()
    {
        var start = Dates.NextMonday(40);

        var employeeOneClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var employeeTwoClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var employeeThreeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var vacationId = await employeeOneClient.GetLeaveTypeIdAsync("VACATION");

        var requestOne = await (await employeeOneClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();
        var requestTwo = await (await employeeTwoClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();
        var requestThree = await (await employeeThreeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(2), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);

        var approveOne = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{requestOne!.Id}/manager-decision/approve", new LeaveDecisionDto(requestOne.RowVersion, null));
        Assert.Equal(HttpStatusCode.OK, approveOne.StatusCode);

        var approveTwo = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{requestTwo!.Id}/manager-decision/approve", new LeaveDecisionDto(requestTwo.RowVersion, null));
        Assert.Equal(HttpStatusCode.OK, approveTwo.StatusCode);

        var approveThree = await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{requestThree!.Id}/manager-decision/approve", new LeaveDecisionDto(requestThree.RowVersion, null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, approveThree.StatusCode);

        var stillPending = await employeeThreeClient.GetFromJsonAsync<LeaveRequestResponse>($"/api/leave-requests/{requestThree.Id}");
        Assert.Equal("PendingManager", stillPending!.Status);
    }
}
