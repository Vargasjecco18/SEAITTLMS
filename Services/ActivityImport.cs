using SEAITT_LMS.Components.Shared;

namespace SEAITT_LMS.Services;

/// <summary>One thing of the workbook that is ready to be saved, with the files it named (already read).</summary>
public sealed record ActivityToSave(ActivityWork Work, IReadOnlyList<NewAttachment> Files, string Sheet, int Row);

/// <summary>
/// Saves what the Excel workbook of activities describes into a module. The teacher's course page and the provider's course library both use it, each giving the three steps
/// with its own rules (who may save, where the files go): the item, its questions (a quiz) and its files.
/// </summary>
public static class ActivityImport
{
    public static async Task<ItemImportSummary> RunAsync(
        IReadOnlyList<ActivityToSave> list,
        Func<ItemInput, Task<ServiceResult>> saveItem,
        Func<int, IReadOnlyList<QuestionInput>, Task<ServiceResult>> saveQuestions,
        Func<int, IReadOnlyList<NewAttachment>, Task<ServiceResult>> attach)
    {
        var created = 0;
        var problems = new List<string>();
        foreach (var entry in list)
        {
            var work = entry.Work;
            var where = $"{entry.Sheet}, row {entry.Row} ({work.Input.Title})";
            var saved = await saveItem(work.Input);
            if (!saved.Succeeded || saved.Id is not { } id)
            {
                problems.Add($"{where}: {string.Join(" ", saved.Errors)}");
                continue;
            }

            created++;
            if (work.Questions.Count > 0)
            {
                var questions = await saveQuestions(id, work.Questions);
                if (!questions.Succeeded)
                {
                    problems.Add($"{where}: it was added, but not its questions: {string.Join(" ", questions.Errors)}");
                }
            }

            if (entry.Files.Count > 0)
            {
                var files = await attach(id, entry.Files);
                if (!files.Succeeded)
                {
                    problems.Add($"{where}: it was added, but not its files: {string.Join(" ", files.Errors)}");
                }
            }
        }

        return new ItemImportSummary(created, problems);
    }
}
