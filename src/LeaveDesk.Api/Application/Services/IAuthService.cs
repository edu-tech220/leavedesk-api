using LeaveDesk.Api.Application.Dtos;

namespace LeaveDesk.Api.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
