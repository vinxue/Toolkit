namespace Nexus.Core
{
    /// <summary>
    /// Identifies the WebView2 profile a control is created with. In-private
    /// profiles keep nothing on disk once the last window using them closes.
    /// </summary>
    public readonly record struct ProfileContext(string Name, bool IsInPrivate);
}
