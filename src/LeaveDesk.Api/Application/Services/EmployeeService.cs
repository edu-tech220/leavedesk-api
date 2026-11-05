using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Domain.Entities;
using LeaveDesk.Api.Domain.Exceptions;
using LeaveDesk.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Application.Services;

public class EmployeeService : IEmployeeService
{
    private readonly LeaveDeskDbContext _context;

    public EmployeeService(LeaveDeskDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await Query().FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException($"No existe el empleado {id}.");

        return ToResponse(employee);
    }

    public async Task<IReadOnlyList<EmployeeResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var employees = await Query().OrderBy(e => e.FullName).ToListAsync(cancellationToken);
        return employees.Select(ToResponse).ToList();
    }

    private IQueryable<Employee> Query() => _context.Employees
        .Include(e => e.Department)
        .Include(e => e.Manager)
        .AsNoTracking();

    private static EmployeeResponse ToResponse(Employee e) => new(
        e.Id, e.FullName, e.Email, e.Role.ToString(), e.DepartmentId,
        e.Department?.Name ?? string.Empty, e.ManagerId, e.Manager?.FullName);
}
