using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly LeaveDeskDbContext _context;

    public DepartmentsController(LeaveDeskDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse(d.Id, d.Name, d.MaxConcurrentLeaves))
            .ToListAsync(cancellationToken);

        return Ok(departments);
    }
}
