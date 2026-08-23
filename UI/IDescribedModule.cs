namespace Peeker.UI
{
    /// <summary>
    /// Optional opt-in for <c>Peeker.Module.Module</c> subclasses that want the
    /// detail panel to show real flavor text instead of a generic fallback.
    /// The base Module class doesn't carry a description, so this stays a
    /// separate interface rather than a required override.
    /// </summary>
    public interface IDescribedModule
    {
        string Description { get; }
    }
}
