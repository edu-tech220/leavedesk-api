using System.Net;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Tests.Infrastructure;
using Xunit;

namespace LeaveDesk.Api.Tests;

public class LeaveBalanceTests : IClassFixture<LeaveDeskWebApplicationFactory>
{
    private readonly LeaveDeskWebApplicationFactory _factory;

    public LeaveBalanceTests(LeaveDeskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_RegresaSaldoInicialCompleto()
    {
        var client = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);

        var balances = await client.GetFromJsonAsync<List<LeaveBalanceResponse>>("/api/leave-balances/me");

        Assert.NotNull(balances);
        var vacation = balances!.Single(b => b.LeaveTypeCode == "VACATION");
        Assert.Equal(12, vacation.AccruedDays);
        Assert.Equal(0, vacation.UsedDays);
        Assert.Equal(12, vacation.AvailableDays);
    }

    [Fact]
    public async Task GetDeOtroEmpleado_SinRelacion_Regresa403()
    {
        var employeeClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne);
        var otherId = await (await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeTwo)).GetEmployeeIdAsync();

        var response = await employeeClient.GetAsync($"/api/leave-balances/{otherId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_PuedeVerSaldoDeSuReporteDirecto()
    {
        var employeeId = await (await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne)).GetEmployeeIdAsync();
        var managerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Manager);

        var response = await managerClient.GetAsync($"/api/leave-balances/{employeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_NoPuedeVerSaldoDeQuienNoLeReporta()
    {
        var employeeId = await (await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne)).GetEmployeeIdAsync();
        var otherManagerClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.OtherManager);

        var response = await otherManagerClient.GetAsync($"/api/leave-balances/{employeeId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Hr_PuedeVerSaldoDeCualquierEmpleado()
    {
        var employeeId = await (await _factory.CreateClient().AuthenticateAsAsync(TestUsers.EmployeeOne)).GetEmployeeIdAsync();
        var hrClient = await _factory.CreateClient().AuthenticateAsAsync(TestUsers.Hr);

        var response = await hrClient.GetAsync($"/api/leave-balances/{employeeId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
