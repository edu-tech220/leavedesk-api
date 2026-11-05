namespace LeaveDesk.Api.Application.Dtos;

public record EmployeeResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    int DepartmentId,
    string DepartmentName,
    Guid? ManagerId,
    string? ManagerName);
