namespace SmartDrag.Windows.Probe;

internal enum ProbeMode
{
    /// <summary>
    /// Interop-only proof. May show an overlay from an Explorer drag signal before payload support is known.
    /// This mode MUST NEVER be copied into production behavior.
    /// </summary>
    P0SignalOnly = 0,

    /// <summary>
    /// G2 experiment. Requires a conservative Explorer selected-path snapshot before showing the overlay,
    /// then revalidates the actual OLE payload on Drop.
    /// </summary>
    G2ExplorerSelection
}
