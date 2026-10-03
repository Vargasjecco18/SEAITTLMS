namespace SEAITT_LMS.Data.Entities;

/// <summary>
/// Base class for tables that carry an auto-increment id and created/updated timestamps.
/// It is intentionally not part of the EF model itself, its properties are mapped onto each
/// derived table. AppDbContext.SaveChanges fills in the timestamps (always UTC).
/// </summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
