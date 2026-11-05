using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveDesk.Api.Controllers;

[ApiController]
[Route("api/leave-balances")]
[Authorize]
public class LeaveBalancesController : ControllerBase
{
    private readonly ILeaveBalanceService _balanceService;
    private readonly ICurrentUser _currentUser;

    public LeaveBalancesController(ILeaveBalanceService balanceService, ICurrentUser currentUser)
    {
        _balanceService = balanceService;
        _currentUser = currentUser;
    }

    [HttpGet("{employeeId:guid}")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceResponse>>> GetForEmployee(
        Guid employeeId, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var balances = await _balanceService.GetForEmployeeAsync(employeeId, _currentUser, year, cancellationToken);
        return Ok(balances);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<LeaveBalanceResponse>>> GetMine(
        [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var balances = await _balanceService.GetForEmployeeAsync(_currentUser.EmployeeId, _currentUser, year, cancellationToken);
        return Ok(balances);
    }
}
