namespace SeatingPlanner.Modules.Events.Domain;

internal sealed class Event
{
    public const int NameMaxLength = 100;

    private Event()
    {
    }

    private Event(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public static Event Create(string name) => Create(Guid.CreateVersion7(), name);

    internal static Event Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));

        return new Event(id, name);
    }
}
