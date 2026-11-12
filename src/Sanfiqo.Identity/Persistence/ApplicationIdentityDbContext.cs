using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;

namespace Sanfiqo.Identity;

public sealed class ApplicationIdentityDbContext(DbContextOptions<ApplicationIdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<OtpSendEvent> OtpSendEvents => Set<OtpSendEvent>();
    public DbSet<OtpWriteGate> OtpWriteGates => Set<OtpWriteGate>();
    public DbSet<UserContact> UserContacts => Set<UserContact>();
    public DbSet<UserContactProofRecord> UserContactProofRecords => Set<UserContactProofRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Explicit lengths keep composite utf8mb4 indexes within MySQL's byte limits.
        // Match runtime Identity key options and retain the framework relationships/stamps.
        foreach (var type in builder.Model.GetEntityTypes())
            foreach (var property in type.GetProperties().Where(p => p.ClrType == typeof(string)
                && (p.IsPrimaryKey() || p.IsForeignKey())))
            {
                property.SetMaxLength(128);
                builder.Entity(type.ClrType).Property(property.Name)
                    .ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation("utf8mb4_bin");
            }

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FullName).HasMaxLength(200);
            user.Property(u => u.PreferredLanguage).HasMaxLength(8).IsRequired();
            user.Property(u => u.IsActive).HasDefaultValue(false);
            user.Property(u => u.CreatedAtUtc).HasColumnType("datetime(6)");
            user.Property(u => u.LastLoginAtUtc).HasColumnType("datetime(6)");
            user.Property(u => u.NormalizedUserName).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation("utf8mb4_bin");
            user.Property(u => u.NormalizedEmail).ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation("utf8mb4_bin");
        });
        builder.Entity<ApplicationRole>().Property(r => r.NormalizedName)
            .ForMySQLHasCharset("utf8mb4").ForMySQLHasCollation("utf8mb4_bin");
        builder.Entity<OtpChallenge>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.MobileHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.CodeHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.ExpiresAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.ConsumedAtUtc).HasColumnType("datetime(6)");
            entity.HasIndex(x => new { x.State, x.ExpiresAtUtc });
        });
        builder.Entity<OtpSendEvent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MobileHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.IpHash).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnType("datetime(6)");
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.ChallengeId).IsUnique();
        });
        builder.Entity<OtpWriteGate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasData(new OtpWriteGate { Id = 1 });
        });
        builder.Entity<UserContact>(entity =>
        {
            entity.ToTable("UserContacts", table => table.HasCheckConstraint("CK_UserContacts_VerificationState",
                "(IsVerified = 0 AND OwnershipKey IS NULL AND VerifiedAtUtc IS NULL) OR "
                + "(IsVerified = 1 AND OwnershipKey IS NOT NULL AND OwnershipKey = LookupKey AND VerifiedAtUtc IS NOT NULL)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnType("char(36)").ValueGeneratedNever();
            // Shared Identity key mapping above supplies varchar(128)/utf8mb4_bin for this FK.
            entity.Property(x => x.UserId).HasMaxLength(128).HasColumnType("varchar(128)").IsRequired();
            entity.Property(x => x.Kind).HasConversion<byte>().HasColumnType("tinyint unsigned");
            entity.Property(x => x.LookupKey).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.OwnershipKey).HasColumnType("binary(32)");
            entity.Property(x => x.ProtectedValue).HasColumnType("varbinary(2048)").IsRequired();
            entity.Property(x => x.IsVerified).HasDefaultValue(false);
            entity.Property(x => x.IsPrimary).HasDefaultValue(false);
            entity.Property(x => x.VerifiedAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.Version).HasDefaultValue(1).IsConcurrencyToken();
            entity.HasIndex(x => new { x.Kind, x.OwnershipKey }).IsUnique()
                .HasDatabaseName("UX_UserContacts_VerifiedOwner");
            entity.HasIndex(x => new { x.UserId, x.Kind, x.IsPrimary })
                .HasDatabaseName("IX_UserContacts_User_Kind_Primary");
            entity.HasIndex(x => new { x.UserId, x.Kind, x.LookupKey, x.IsVerified })
                .HasDatabaseName("IX_UserContacts_User_Lookup");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_UserContacts_AspNetUsers_UserId");
        });
        builder.Entity<UserContactProofRecord>(entity =>
        {
            entity.ToTable("UserContactVerificationProofs", table => table.HasCheckConstraint(
                "CK_UserContactProofs_Expiry", "ExpiresAtUtc > IssuedAtUtc"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnType("char(36)").ValueGeneratedNever();
            entity.Property(x => x.SourceVerificationId).HasColumnType("char(36)");
            entity.Property(x => x.Kind).HasConversion<byte>().HasColumnType("tinyint unsigned");
            entity.Property(x => x.LookupKey).HasColumnType("binary(32)").IsRequired();
            entity.Property(x => x.IssuedAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.ExpiresAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.ConsumedAtUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.Purpose).HasMaxLength(64).HasColumnType("varchar(64)").IsRequired();
            entity.HasIndex(x => x.SourceVerificationId).IsUnique()
                .HasDatabaseName("UX_UserContactProofs_SourceVerification");
            entity.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_UserContactProofs_ExpiresAt");
        });
        // EmailIndex remains non-unique, as in Identity. Contact linking/uniqueness policy
        // must be reviewed with the real registration workflow rather than invented here.
    }
}
