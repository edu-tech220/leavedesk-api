using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Dtos;

namespace LeaveDesk.Api.Application.Services;

public interface ILeaveBalanceService
{
    Task<IReadOnlyList<LeaveBalanceResponse>> GetForEmployeeAsync(
        Guid employeeId, ICurrentUser user, int? year, CancellationToken cancellationToken = default);
}
