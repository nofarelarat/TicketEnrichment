using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupportTickets.Api.Domain;

namespace SupportTickets.Api.Persistence;

public class TicketDbContext : DbContext
{
    public TicketDbContext(DbContextOptions<TicketDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite cannot compare or order DateTimeOffset values, and the worker's "due tickets"
        // query needs both, so timestamps are stored as UTC Unix milliseconds.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UnixMillisecondsConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var ticket = modelBuilder.Entity<Ticket>();

        ticket.HasKey(t => t.Id);
        ticket.Property(t => t.Id).HasMaxLength(TicketLimits.IdMaxLength);
        ticket.Property(t => t.Subject).HasMaxLength(TicketLimits.SubjectMaxLength);
        ticket.Property(t => t.Message).HasMaxLength(TicketLimits.MessageMaxLength);
        ticket.Property(t => t.Summary).HasMaxLength(TicketLimits.SummaryMaxLength);
        ticket.Property(t => t.LastError).HasMaxLength(Ticket.MaxLastErrorLength);
        ticket.Property(t => t.EnrichmentStatus).HasConversion<string>().HasMaxLength(16);
        ticket.Property(t => t.Category).HasConversion<string>().HasMaxLength(32);
        ticket.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16);

        // Serves the worker's polling query.
        ticket.HasIndex(t => new { t.EnrichmentStatus, t.NextAttemptAt });
    }

    private class UnixMillisecondsConverter : ValueConverter<DateTimeOffset, long>
    {
        public UnixMillisecondsConverter()
            : base(
                value => value.ToUnixTimeMilliseconds(),
                value => DateTimeOffset.FromUnixTimeMilliseconds(value))
        {
        }
    }
}
