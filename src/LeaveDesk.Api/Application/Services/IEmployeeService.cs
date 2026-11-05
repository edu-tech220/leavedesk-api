using LeaveDesk.Api.Application.Dtos;

namespace LeaveDesk.Api.Application.Services;

public interface IEmployeeService
{
    Task<EmployeeResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
