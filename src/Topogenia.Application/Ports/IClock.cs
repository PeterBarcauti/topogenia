// IClock.cs
// Port of the time of the server: the only source of the current UTC time outside the domain (D08, spec 3.4).

namespace Topogenia.Application.Ports;

/// <summary>Current time of the server, always UTC (D08). Tests give a fixed clock.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
