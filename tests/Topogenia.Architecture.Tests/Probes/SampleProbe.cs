// SampleProbe.cs
// Deliberately faulty pair of types: a test probe and a type outside the probes that uses it, so that the rule of the probes is proven to fire.

namespace Topogenia.Architecture.Tests.Probes;

/// <summary>Stands for a test probe of a provider (`*Probe`, ADR 037).</summary>
internal static class SampleOptionsProbe
{
    public static int Level => 1;
}

/// <summary>Probe for <c>ProbeRulesTests</c>: a type that is not a probe and depends on one, which the rule refuses.</summary>
internal static class SampleProbeUser
{
    public static int Level => SampleOptionsProbe.Level + 1;
}
