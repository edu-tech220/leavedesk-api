using LeaveDesk.Api.Domain.Enums;

namespace LeaveDesk.Api.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public Role Role { get; set; }
    public DateTime HireDateUtc { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    /// <summary>
    /// Gerente directo que resuelve el primer nivel de aprobacion. Nulo para Admin/HR raiz.
    /// </summary>
    public Guid? ManagerId { get; set; }
    public Employee? Manager { get; set; }

    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<LeaveBalance> LeaveBalances { get; set; } = new List<LeaveBalance>();
}
