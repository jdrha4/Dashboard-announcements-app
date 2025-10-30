using Application.Infrastructure.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Infrastructure.Database;

public class DatabaseContext : DbContext
{
    public DbSet<UserDo> Users => Set<UserDo>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<UserDashboardMap> UserDashboards => Set<UserDashboardMap>();
    public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();
    public DbSet<AllowedDomain> AllowedDomains => Set<AllowedDomain>();
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens { get; set; } = null!;
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<DashboardPreviewPin> DashboardPreviewPins => Set<DashboardPreviewPin>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollChoice> PollChoices => Set<PollChoice>();
    public DbSet<PollVote> PollVotes => Set<PollVote>();
    public DbSet<DashboardAnnouncementMap> DashboardAnnouncements => Set<DashboardAnnouncementMap>();

    public DatabaseContext(DbContextOptions options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Each announcement belongs to one user; delete announcements when user is deleted
        modelBuilder
            .Entity<Announcement>()
            .HasOne(a => a.User)
            .WithMany(u => u.Announcements)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Legacy one-to-many: Each announcement can optionally belong to one dashboard
        modelBuilder
            .Entity<Announcement>()
            .HasOne(a => a.Dashboard)
            .WithMany(d => d.Announcements)
            .HasForeignKey(a => a.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);

        // A dashboard has an author, but no backreference from the author (user) to dashboards
        // Prevent deleting a user if they authored a dashboard
        modelBuilder
            .Entity<Dashboard>()
            .HasOne(d => d.Author)
            .WithMany()
            .HasForeignKey(d => d.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Many-to-many relationship between users and dashboards (map table)
        modelBuilder.Entity<UserDashboardMap>().HasKey(ud => new { ud.UserId, ud.DashboardId });

        modelBuilder
            .Entity<UserDashboardMap>()
            .HasOne(ud => ud.User)
            .WithMany(u => u.UserDashboards)
            .HasForeignKey(ud => ud.UserId);

        modelBuilder
            .Entity<UserDashboardMap>()
            .HasOne(ud => ud.Dashboard)
            .WithMany(d => d.UserDashboards)
            .HasForeignKey(ud => ud.DashboardId);

        // Many-to-many relationship between dashboards and announcements
        modelBuilder.Entity<DashboardAnnouncementMap>().HasKey(da => new { da.DashboardId, da.AnnouncementId });

        modelBuilder
            .Entity<DashboardAnnouncementMap>()
            .HasOne(da => da.Dashboard)
            .WithMany(d => d.DashboardAnnouncements)
            .HasForeignKey(da => da.DashboardId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder
            .Entity<DashboardAnnouncementMap>()
            .HasOne(da => da.Announcement)
            .WithMany(a => a.DashboardAnnouncements)
            .HasForeignKey(da => da.AnnouncementId);

        // One-to-many: GlobalSettings -> AllowedDomain
        modelBuilder
            .Entity<GlobalSettings>()
            .HasMany(g => g.AllowedEmailDomains)
            .WithOne(d => d.GlobalSettings)
            .HasForeignKey(d => d.GlobalSettingsId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate domains within the same GlobalSettings group
        modelBuilder.Entity<AllowedDomain>().HasIndex(a => new { a.Domain, a.GlobalSettingsId }).IsUnique();

        // Each comment is made by a user (no backref); don't allow deleting user with comments
        modelBuilder
            .Entity<Comment>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comments are deleted if their announcement is deleted
        modelBuilder
            .Entity<Comment>()
            .HasOne(c => c.Announcement)
            .WithMany(a => a.Comments)
            .HasForeignKey(c => c.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many relationship: Dashboard -> DashboardPreviewPin
        modelBuilder
            .Entity<DashboardPreviewPin>()
            .HasOne<Dashboard>()
            .WithMany()
            .HasForeignKey(p => p.DashboardId)
            .IsRequired();

        // One-to-one announcement-Poll relation
        modelBuilder
            .Entity<Poll>()
            .HasOne(p => p.Announcement)
            .WithOne()
            .HasForeignKey<Poll>(p => p.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many relation between Poll and PollChoice
        modelBuilder
            .Entity<PollChoice>()
            .HasOne(pc => pc.Poll)
            .WithMany(p => p.PollChoices)
            .HasForeignKey(pc => pc.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many relation between PollChoice and PollVote (user voting on a choice)
        modelBuilder
            .Entity<PollChoice>()
            .HasMany(pc => pc.PollVotes)
            .WithOne(pv => pv.PollChoice)
            .HasForeignKey(pv => pv.PollChoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // One-to-many relation between PollVote and User
        modelBuilder
            .Entity<PollVote>()
            .HasOne(pv => pv.User)
            .WithMany()
            .HasForeignKey(pv => pv.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
