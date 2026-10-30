using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SEAITT_LMS.Services;

namespace SEAITT_LMS.Components.Pages.Coordinator.Setup;

/// <summary>What the tabs of the academic setup have in common: the service, the signed-in academic coordinator, the message line and the errors of a form.</summary>
public abstract class SetupTabBase : ComponentBase
{
    [Inject] protected SchoolSetupService Setup { get; set; } = null!;

    [Inject] protected CurrentUser Me { get; set; } = null!;

    [Inject] protected IJSRuntime JS { get; set; } = null!;

    /// <summary>The id of the box with the fields of the tab (the fields are always on the page).</summary>
    protected string EntryId => "entry-" + GetType().Name;

    /// <summary>Edit on a row puts the row in the fields; this brings the fields into view and puts the cursor in the first one.</summary>
    protected async Task ShowFormAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("seaitt.showEntry", EntryId);
        }
        catch (JSException)
        {
            // The fields are filled in anyway; only the scrolling is missed.
        }
        catch (InvalidOperationException)
        {
            // No browser to talk to (the page is closing).
        }
    }

    /// <summary>Called after something was saved or deleted, so the page can refresh the numbers on its tabs.</summary>
    [Parameter] public EventCallback OnChanged { get; set; }

    protected string? Message { get; set; }

    protected bool MessageIsError { get; set; }

    protected List<string> Errors { get; set; } = [];

    protected bool Busy { get; set; }

    protected void ClearMessage() => Message = null;

    /// <summary>
    /// Runs a change as the signed-in administrator. A success becomes the message at the top of the tab; the problems of a failure
    /// are kept in <see cref="Errors"/> for the box that is open.
    /// </summary>
    protected async Task<ServiceResult> RunAsync(Func<int, Task<ServiceResult>> action)
    {
        if (Busy)
        {
            return ServiceResult.Fail("Please wait a moment.");
        }

        Busy = true;
        Errors = [];
        try
        {
            var me = await Me.GetAsync();
            var result = me is null ? ServiceResult.Fail("Please sign in again.") : await action(me.UserId);
            if (result.Succeeded)
            {
                Message = result.Message;
                MessageIsError = false;
                await OnChanged.InvokeAsync();
            }
            else
            {
                Errors = result.Errors.ToList();
            }

            return result;
        }
        finally
        {
            Busy = false;
        }
    }
}
