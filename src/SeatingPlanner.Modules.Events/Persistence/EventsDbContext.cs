using Microsoft.EntityFrameworkCore;
using SeatingPlanner.Modules.Events.Domain;

namespace SeatingPlanner.Modules.Events.Persistence;

internal sealed class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    public const string Schema = "events";

    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.Property(e => e.Name)
                .HasMaxLength(Event.NameMaxLength)
                .IsRequired();
        });
    }
}
