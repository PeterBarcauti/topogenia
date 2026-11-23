// DesktopWrites.cs
// What the pages of the desktop mode declare still waiting to reach the host (a typing of a sheet, a write of a document) and the forms they hold changed and not validated, held in memory, and the bounded wait of « Quit » by the icon near the clock (F-204, ADR 115, R11).

using Microsoft.Extensions.Options;
using Topogenia.Domain.Errors;
using Topogenia.Host.Composition;

namespace Topogenia.Host.Desktop;

/// <summary>
/// `POST /api/desktop/writes` (ADR 115): a tab of the desktop mode says whether it still has writes to send to the host
/// and, `unsaved` (R11, absent read false), whether it holds a form changed and not validated, which never leaves by itself.
/// </summary>
public sealed record DesktopWritesBody(string? Tab, bool? Waiting, bool? Unsaved = null);

/// <summary>
/// The tabs of the desktop mode that have declared a write still waiting (ADR 115) or a form not validated (R11): each tab
/// sends its identifier (letters, digits and hyphens, <see cref="MaxTabLength"/> characters at most), whether something
/// waits and whether a form is not validated, at each change. <see cref="MaxTabs"/> tabs are held at most, a tab that says
/// both counting once: one more is refused, so that a page cannot make the sets grow without end. Nothing is written to the
/// base nor logged but the line of the request: the sets live as long as the process. « Quit » by the icon waits until no
/// tab waits (<see cref="WaitAsync"/>) and asks at once when a form is not validated (<see cref="Unsaved"/>).
/// </summary>
public sealed class DesktopWrites
{
    /// <summary>The longest identifier of a tab (a `crypto.randomUUID()` of the page has 36 characters).</summary>
    public const int MaxTabLength = 64;

    /// <summary>The most tabs that wait at once.</summary>
    public const int MaxTabs = 32;

    private readonly Lock gate = new();
    private readonly HashSet<string> tabs = new(StringComparer.Ordinal);
    private readonly HashSet<string> forms = new(StringComparer.Ordinal);
    private TaskCompletionSource none = Done();

    /// <summary>The number of tabs that wait.</summary>
    public int Waiting
    {
        get
        {
            lock (gate)
                return tabs.Count;
        }
    }

    /// <summary>The number of tabs that hold a form not validated (R11).</summary>
    public int Unsaved
    {
        get
        {
            lock (gate)
                return forms.Count;
        }
    }

    /// <summary>The tabs of the desktop mode; INTROUVABLE in server mode, which registers none, like the command « Quit ».</summary>
    public static DesktopWrites Of(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.GetRequiredService<IOptions<ProductOptions>>().Value.Mode != HostMode.Desktop)
            throw DomainException.NotFound();
        return services.GetRequiredService<DesktopWrites>();
    }

    /// <summary>
    /// Records what a tab declares: CHAMP_OBLIGATOIRE without its identifier or its state, TEXTE_TROP_LONG past
    /// <see cref="MaxTabLength"/>, VALEUR_INVALIDE on a character other than a letter, a digit or a hyphen, and on a tab
    /// that would be held beyond <see cref="MaxTabs"/>. A tab declared twice, or with both a write and a form, counts once;
    /// a tab unknown that has nothing to send nor any form changes nothing.
    /// </summary>
    public void Declare(DesktopWritesBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var tab = string.IsNullOrEmpty(body.Tab) ? throw DomainException.Invalid(ErrorCodes.RequiredField, "tab") : body.Tab;
        var waiting = body.Waiting ?? throw DomainException.Invalid(ErrorCodes.RequiredField, "waiting");
        var unsaved = body.Unsaved ?? false;
        if (tab.Length > MaxTabLength)
            throw DomainException.Invalid(ErrorCodes.TextTooLong, "tab", MaxTabLength);
        if (!tab.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            throw DomainException.InvalidValue("tab");
        lock (gate)
        {
            if ((waiting || unsaved) && !tabs.Contains(tab) && !forms.Contains(tab) && Held() >= MaxTabs)
                throw DomainException.Invalid(ErrorCodes.InvalidValue, "tab", MaxTabs);
            if (unsaved)
                forms.Add(tab);
            else
                forms.Remove(tab);
            if (!waiting)
            {
                if (tabs.Remove(tab) && tabs.Count == 0)
                    none.TrySetResult();
                return;
            }
            if (tabs.Add(tab) && tabs.Count == 1)
                none = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>The tabs held, a tab with both a write and a form once; under the lock.</summary>
    private int Held() => tabs.Count + forms.Count(form => !tabs.Contains(form));

    /// <summary>True once no tab waits, within <paramref name="bound"/>; false when a tab still waits at its end.</summary>
    public async Task<bool> WaitAsync(TimeSpan bound, CancellationToken cancellationToken)
    {
        Task empty;
        lock (gate)
            empty = none.Task;
        try
        {
            await empty.WaitAsync(bound, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private static TaskCompletionSource Done()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        done.SetResult();
        return done;
    }
}
