using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Controllers;

[ApiController]
[Route("api/leave-types")]
[Authorize]
public class LeaveTypesController : ControllerBase
{
    private readonly LeaveDeskDbContext _context;

    public LeaveTypesController(LeaveDeskDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeaveTypeResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var leaveTypes = await _context.LeaveTypes
            .AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t => new LeaveTypeResponse(t.Id, t.Code, t.Name, t.AnnualDays, t.TracksBalance, t.AlwaysRequiresHrApproval))
            .ToListAsync(cancellationToken);

        return Ok(leaveTypes);
    }
}
