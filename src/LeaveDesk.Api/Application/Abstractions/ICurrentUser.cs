using LeaveDesk.Api.Domain.Enums;

namespace LeaveDesk.Api.Application.Abstractions;

public interface ICurrentUser
{
    Guid EmployeeId { get; }
    Role Role { get; }
    int DepartmentId { get; }
}
