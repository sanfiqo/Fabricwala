using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TextileConnect.Domain.Payments;

namespace TextileConnect.Infrastructure.Persistence;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditRows(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    private static void AddAuditRows(DbContext? context)
    {
        if (context is null) return;
        var entries = context.ChangeTracker.Entries().Where(x => x.Entity is not AuditLog && x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in entries)
            context.Set<AuditLog>().Add(new AuditLog { Action = entry.State.ToString(), EntityName = entry.Metadata.ClrType.Name, EntityId = entry.Properties.FirstOrDefault(x => x.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() });
    }
}
