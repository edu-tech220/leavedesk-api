namespace LeaveDesk.Api.Domain.Entities;

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Numero maximo de empleados del departamento que pueden tener un permiso
    /// aprobado con fechas que se solapen al mismo tiempo.
    /// </summary>
    public int MaxConcurrentLeaves { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
