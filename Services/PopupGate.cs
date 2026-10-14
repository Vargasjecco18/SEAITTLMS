namespace SEAITT_LMS.Services;

/// <summary>
/// One pop-up in the middle of the screen at a time. A box that celebrates something (an achievement) takes the gate while it is open; the other boxes of the
/// same person (the message of the guidance counselor) wait until it is released, so two boxes never sit on top of each other. One for each open page (scoped).
/// </summary>
public sealed class PopupGate
{
    private int holders;

    /// <summary>True while a box that goes first is open.</summary>
    public bool Busy => holders > 0;

    /// <summary>Said when the gate was taken or released, so a box that waits can look again.</summary>
    public event Action? Changed;

    public void Enter()
    {
        holders++;
        Changed?.Invoke();
    }

    public void Leave()
    {
        if (holders > 0)
        {
            holders--;
        }

        Changed?.Invoke();
    }
}
