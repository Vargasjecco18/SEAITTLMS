using Microsoft.EntityFrameworkCore;
using SEAITT_LMS.Components.Shared;
using SEAITT_LMS.Data;
using SEAITT_LMS.Data.Entities;

namespace SEAITT_LMS.Services;

public sealed record TodoEntry(int Id, string Title, DateOnly? DueOn, bool IsDone, bool IsOverdue);

public sealed record TodoList(IReadOnlyList<TodoEntry> Open, IReadOnlyList<TodoEntry> Done);

/// <summary>A person's own to-do list. Everything is scoped to the account: nobody can read or change somebody else's list.</summary>
public sealed class TodoService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider clock)
{
    private const int MaxOpen = 100;

    public async Task<TodoList> GetAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var today = PhilippineTime.Today(clock);

        var open = await db.TodoItems.AsNoTracking().Where(t => t.UserId == userId && !t.IsDone)
            .OrderBy(t => t.DueOn == null).ThenBy(t => t.DueOn).ThenBy(t => t.Id)
            .Select(t => new TodoEntry(t.Id, t.Title, t.DueOn, false, t.DueOn != null && t.DueOn < today))
            .ToListAsync(ct);
        var done = await db.TodoItems.AsNoTracking().Where(t => t.UserId == userId && t.IsDone)
            .OrderByDescending(t => t.DoneAtUtc).ThenByDescending(t => t.Id).Take(10)
            .Select(t => new TodoEntry(t.Id, t.Title, t.DueOn, true, false))
            .ToListAsync(ct);
        return new TodoList(open, done);
    }

    public async Task<ServiceResult> AddAsync(int userId, string? title, DateOnly? dueOn, CancellationToken ct = default)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return ServiceResult.Fail("Write what you have to do.");
        }

        if (title.Length > 200)
        {
            return ServiceResult.Fail("The title can have at most 200 characters.");
        }

        if (dueOn is { } due && (due.Year < 2000 || due.Year > 2100))
        {
            return ServiceResult.Fail("Pick a real date.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.TodoItems.CountAsync(t => t.UserId == userId && !t.IsDone, ct) >= MaxOpen)
        {
            return ServiceResult.Fail($"You already have {MaxOpen} things to do. Finish or delete some first.");
        }

        var item = new TodoItem { UserId = userId, Title = title, DueOn = dueOn };
        db.TodoItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok("Added.", item.Id);
    }

    /// <summary>Checks an entry off, or puts it back.</summary>
    public async Task<ServiceResult> SetDoneAsync(int userId, int id, bool done, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = await db.TodoItems.Where(t => t.Id == id && t.UserId == userId)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.IsDone, done).SetProperty(t => t.DoneAtUtc, done ? now : (DateTime?)null), ct);
        return changed == 0 ? ServiceResult.Fail("This entry was not found.") : ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var deleted = await db.TodoItems.Where(t => t.Id == id && t.UserId == userId).ExecuteDeleteAsync(ct);
        return deleted == 0 ? ServiceResult.Fail("This entry was not found.") : ServiceResult.Ok();
    }

    public async Task ClearDoneAsync(int userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.TodoItems.Where(t => t.UserId == userId && t.IsDone).ExecuteDeleteAsync(ct);
    }
}
