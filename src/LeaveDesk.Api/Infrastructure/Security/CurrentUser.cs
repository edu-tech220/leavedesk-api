using System.Security.Claims;
using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Domain.Exceptions;

namespace LeaveDesk.Api.Infrastructure.Security;

public class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User
            ?? throw new ForbiddenException("No hay un usuario autenticado en el contexto actual.");

        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = user.FindFirstValue(ClaimTypes.Role);
        var departmentId = user.FindFirstValue("departmentId");

        if (id is null || role is null || departmentId is null)
        {
            throw new ForbiddenException("El token no contiene los claims requeridos.");
        }

        EmployeeId = Guid.Parse(id);
        Role = Enum.Parse<Role>(role);
        DepartmentId = int.Parse(departmentId);
    }

    public Guid EmployeeId { get; }
    public Role Role { get; }
    public int DepartmentId { get; }
}
