using GymBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace GymBackend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    public DbSet<User> Users => Set<User>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<ScheduleSlot> ScheduleSlots => Set<ScheduleSlot>();

    public DbSet<Attendance> Attendances => Set<Attendance>();
}

