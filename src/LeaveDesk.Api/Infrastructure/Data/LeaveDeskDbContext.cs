using LeaveDesk.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeaveDesk.Api.Infrastructure.Data;

public class LeaveDeskDbContext : DbContext
{
    public LeaveDeskDbContext(DbContextOptions<LeaveDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveRequestAudit> LeaveRequestAudits => Set<LeaveRequestAudit>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RefreshConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RefreshConcurrencyTokens();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RefreshConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IHasConcurrencyToken>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(d => d.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(200);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();

            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveType>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Code).IsRequired().HasMaxLength(30);
            entity.HasIndex(t => t.Code).IsUnique();
            entity.Property(t => t.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<LeaveBalance>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.AccruedDays).HasColumnType("decimal(5,1)");
            entity.Property(b => b.UsedDays).HasColumnType("decimal(5,1)");
            entity.Ignore(b => b.AvailableDays);
            entity.Property(b => b.RowVersion).IsConcurrencyToken().HasMaxLength(16);

            entity.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique();

            entity.HasOne(b => b.Employee)
                .WithMany(e => e.LeaveBalances)
                .HasForeignKey(b => b.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(b => b.LeaveType)
                .WithMany()
                .HasForeignKey(b => b.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_LeaveBalances_UsedWithinAccrued",
                "\"UsedDays\" <= \"AccruedDays\""));
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.BusinessDays).HasColumnType("decimal(5,1)");
            entity.Property(r => r.Reason).HasMaxLength(500);
            entity.Property(r => r.ManagerComments).HasMaxLength(500);
            entity.Property(r => r.HrComments).HasMaxLength(500);
            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.RowVersion).IsConcurrencyToken().HasMaxLength(16);

            entity.HasIndex(r => new { r.EmployeeId, r.Status });

            entity.HasOne(r => r.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.LeaveType)
                .WithMany()
                .HasForeignKey(r => r.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(t => t.HasCheckConstraint(
                "CK_LeaveRequests_DateRange",
                "\"StartDate\" <= \"EndDate\""));
        });

        modelBuilder.Entity<LeaveRequestAudit>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.OldStatus).HasMaxLength(20);
            entity.Property(a => a.NewStatus).HasMaxLength(20);
        });
    }
}
