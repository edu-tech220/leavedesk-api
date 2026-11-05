using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Common;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class SearchAndScopeTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public SearchAndScopeTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Search_Paginacion_RespetaTamanoDePaginaYCalculaTotales()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var vacationId = await client.GetLeaveTypeIdAsync("VACATION");

        for (var week = 50; week < 53; week++)
        {
            var start = Dates.NextMonday(week);
            var response = await client.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(vacationId, start, start.AddDays(1), null));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var page = await client.GetFromJsonAsync<PagedResult<LeaveRequestResponse>>("/api/leave-requests?pageSize=2&page=1");

        Assert.NotNull(page);
        Assert.True(page!.Items.Count <= 2);
        Assert.True(page.TotalCount >= 3);
        Assert.Equal(1, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.True(page.TotalPages >= 2);
    }

    [Fact]
    public async Task Search_EmpleadoConsultandoOtroEmpleado_Regresa403()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var otherId = await (await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo)).GetEmployeeIdAsync();

        var response = await client.GetAsync($"/api/leave-requests?employeeId={otherId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PendingApprovals_Gerente_SoloVeASusReportesDirectos()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeThree);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(54);

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, start.AddDays(1), null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        var managerPending = await managerClient.GetFromJsonAsync<PagedResult<LeaveRequestResponse>>("/api/leave-requests/pending-approvals?pageSize=100");
        Assert.Contains(managerPending!.Items, r => r.Id == created!.Id);

        var otherManagerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.OtherManager);
        var otherManagerPending = await otherManagerClient.GetFromJsonAsync<PagedResult<LeaveRequestResponse>>("/api/leave-requests/pending-approvals?pageSize=100");
        Assert.DoesNotContain(otherManagerPending!.Items, r => r.Id == created!.Id);
    }

    [Fact]
    public async Task PendingApprovals_Hr_SoloVeLoQueYaPasoAlGerente()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo);
        var vacationId = await employeeClient.GetLeaveTypeIdAsync("VACATION");
        var start = Dates.NextMonday(55);
        var end = start.AddDays(11); // 10 dias habiles, requiere RH

        var created = await (await employeeClient.PostAsJsonAsync("/api/leave-requests", new CreateLeaveRequestDto(
            vacationId, start, end, null))).Content.ReadFromJsonAsync<LeaveRequestResponse>();

        var hrClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Hr);
        var beforeManager = await hrClient.GetFromJsonAsync<PagedResult<LeaveRequestResponse>>("/api/leave-requests/pending-approvals?pageSize=100");
        Assert.DoesNotContain(beforeManager!.Items, r => r.Id == created!.Id);

        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);
        await managerClient.PutAsJsonAsync(
            $"/api/leave-requests/{created!.Id}/manager-decision/approve", new LeaveDecisionDto(created.RowVersion, null));

        var afterManager = await hrClient.GetFromJsonAsync<PagedResult<LeaveRequestResponse>>("/api/leave-requests/pending-approvals?pageSize=100");
        Assert.Contains(afterManager!.Items, r => r.Id == created.Id);
    }
}
