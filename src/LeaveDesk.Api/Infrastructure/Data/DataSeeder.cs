using LeaveDesk.Api.Domain.Entities;
using LeaveDesk.Api.Domain.Enums;
using LeaveDesk.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Infrastructure.Data;

/// <summary>
/// Carga datos de demostracion: departamentos, tipos de permiso y una jerarquia
/// minima de empleados (admin, RH, gerente y dos colaboradores).
/// </summary>
public static class DataSeeder
{
    public const string DemoPassword = "Passw0rd!";

    public static async Task SeedAsync(LeaveDeskDbContext context, IPasswordHasher passwordHasher)
    {
        if (await context.Departments.AnyAsync())
        {
            return;
        }

        var engineering = new Department { Name = "Ingenieria", MaxConcurrentLeaves = 2 };
        var support = new Department { Name = "Soporte", MaxConcurrentLeaves = 1 };
        context.Departments.AddRange(engineering, support);
        await context.SaveChangesAsync();

        var leaveTypes = new[]
        {
            new LeaveType { Code = "VACATION", Name = "Vacaciones", AnnualDays = 12, TracksBalance = true, AlwaysRequiresHrApproval = false },
            new LeaveType { Code = "SICK", Name = "Incapacidad", AnnualDays = 6, TracksBalance = true, AlwaysRequiresHrApproval = false },
            new LeaveType { Code = "PERSONAL", Name = "Personal", AnnualDays = 3, TracksBalance = true, AlwaysRequiresHrApproval = false },
            new LeaveType { Code = "UNPAID", Name = "Sin goce de sueldo", AnnualDays = 0, TracksBalance = false, AlwaysRequiresHrApproval = true }
        };
        context.LeaveTypes.AddRange(leaveTypes);
        await context.SaveChangesAsync();

        var hireDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        var admin = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Ana Administradora",
            Email = "admin@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Admin,
            DepartmentId = engineering.Id,
            HireDateUtc = hireDate
        };

        var hr = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Hector Rios",
            Email = "hr@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Hr,
            DepartmentId = support.Id,
            HireDateUtc = hireDate
        };

        var manager = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Monica Guerrero",
            Email = "manager@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Manager,
            DepartmentId = engineering.Id,
            HireDateUtc = hireDate
        };

        var otherManager = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Jorge Paredes",
            Email = "manager2@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Manager,
            DepartmentId = support.Id,
            HireDateUtc = hireDate
        };

        context.Employees.AddRange(admin, hr, manager, otherManager);
        await context.SaveChangesAsync();

        var employeeOne = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Eduardo Ramirez",
            Email = "eduardo@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Employee,
            DepartmentId = engineering.Id,
            ManagerId = manager.Id,
            HireDateUtc = hireDate
        };

        var employeeTwo = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Laura Torres",
            Email = "laura@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Employee,
            DepartmentId = engineering.Id,
            ManagerId = manager.Id,
            HireDateUtc = hireDate
        };

        var employeeThree = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Mejia",
            Email = "carlos@leavedesk.test",
            PasswordHash = passwordHasher.Hash(DemoPassword),
            Role = Role.Employee,
            DepartmentId = engineering.Id,
            ManagerId = manager.Id,
            HireDateUtc = hireDate
        };

        context.Employees.AddRange(employeeOne, employeeTwo, employeeThree);
        await context.SaveChangesAsync();
    }
}
