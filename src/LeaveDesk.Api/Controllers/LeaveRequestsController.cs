using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Common;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveDesk.Api.Controllers;

[ApiController]
[Route("api/leave-requests")]
[Authorize]
public class LeaveRequestsController : ControllerBase
{
    private readonly ILeaveRequestService _leaveRequestService;
    private readonly ICurrentUser _currentUser;

    public LeaveRequestsController(ILeaveRequestService leaveRequestService, ICurrentUser currentUser)
    {
        _leaveRequestService = leaveRequestService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<LeaveRequestResponse>> Create([FromBody] CreateLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.CreateAsync(_currentUser, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeaveRequestResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.GetByIdAsync(id, _currentUser, cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<LeaveRequestResponse>>> Search(
        [FromQuery] Guid? employeeId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new LeaveRequestQuery(employeeId, status, from, to, page, pageSize);
        var response = await _leaveRequestService.SearchAsync(_currentUser, query, cancellationToken);
        return Ok(response);
    }

    [HttpGet("pending-approvals")]
    [Authorize(Roles = "Manager,Hr,Admin")]
    public async Task<ActionResult<PagedResult<LeaveRequestResponse>>> GetPendingApprovals(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var response = await _leaveRequestService.GetPendingApprovalsAsync(_currentUser, page, pageSize, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/manager-decision/approve")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<LeaveRequestResponse>> ApproveByManager(
        Guid id, [FromBody] LeaveDecisionDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.ApproveByManagerAsync(id, _currentUser, dto, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/manager-decision/reject")]
    [Authorize(Roles = "Manager,Admin")]
    public async Task<ActionResult<LeaveRequestResponse>> RejectByManager(
        Guid id, [FromBody] LeaveRejectionDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.RejectByManagerAsync(id, _currentUser, dto, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/hr-decision/approve")]
    [Authorize(Roles = "Hr,Admin")]
    public async Task<ActionResult<LeaveRequestResponse>> ApproveByHr(
        Guid id, [FromBody] LeaveDecisionDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.ApproveByHrAsync(id, _currentUser, dto, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{id:guid}/hr-decision/reject")]
    [Authorize(Roles = "Hr,Admin")]
    public async Task<ActionResult<LeaveRequestResponse>> RejectByHr(
        Guid id, [FromBody] LeaveRejectionDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.RejectByHrAsync(id, _currentUser, dto, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await _leaveRequestService.CancelAsync(id, _currentUser, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel-approved")]
    public async Task<ActionResult<LeaveRequestResponse>> CancelApproved(
        Guid id, [FromBody] CancelApprovedLeaveDto dto, CancellationToken cancellationToken)
    {
        var response = await _leaveRequestService.CancelApprovedAsync(id, _currentUser, dto, cancellationToken);
        return Ok(response);
    }
}
