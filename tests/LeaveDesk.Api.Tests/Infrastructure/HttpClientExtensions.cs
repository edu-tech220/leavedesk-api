using System.Net.Http.Headers;
using System.Net.Http.Json;
using LeaveDesk.Api.Application.Dtos;

namespace LeaveDesk.Api.Tests.Infrastructure;

public static class HttpClientExtensions
{
    public static async Task<HttpClient> AuthenticateAsAsync(this HttpClient client, string email, string password = TestUsers.Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    public static async Task<Guid> GetEmployeeIdAsync(this HttpClient client)
    {
        var employee = await client.GetFromJsonAsync<EmployeeResponse>("/api/employees/me");
        return employee!.Id;
    }

    public static async Task<int> GetLeaveTypeIdAsync(this HttpClient client, string code)
    {
        var leaveTypes = await client.GetFromJsonAsync<List<LeaveTypeResponse>>("/api/leave-types");
        return leaveTypes!.Single(t => t.Code == code).Id;
    }
}
