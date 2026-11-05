namespace Factures.Core.Abstractions;

/// <summary>Date du jour injectee : les regles de retard se testent sans attendre.</summary>
public interface IClock
{
    DateOnly Today { get; }
}

public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
