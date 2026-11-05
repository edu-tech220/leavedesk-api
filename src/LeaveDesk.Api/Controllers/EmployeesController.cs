using LeaveDesk.Api.Application.Abstractions;
using LeaveDesk.Api.Application.Dtos;
using LeaveDesk.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveDesk.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ICurrentUser _currentUser;

    public EmployeesController(IEmployeeService employeeService, ICurrentUser currentUser)
    {
        _employeeService = employeeService;
        _currentUser = currentUser;
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeResponse>> GetMe(CancellationToken cancellationToken)
    {
        var employee = await _employeeService.GetByIdAsync(_currentUser.EmployeeId, cancellationToken);
        return Ok(employee);
    }

    [HttpGet]
    [Authorize(Roles = "Hr,Admin")]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var employees = await _employeeService.GetAllAsync(cancellationToken);
        return Ok(employees);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Manager,Hr,Admin")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _employeeService.GetByIdAsync(id, cancellationToken);
        return Ok(employee);
    }
}
