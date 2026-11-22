using SeatingPlanner.Modules.Events.Domain;

namespace SeatingPlanner.Modules.Events.Tests.Domain;

public sealed class EventTests
{
    [Fact]
    public void Create_WithValidName_ReturnsEventWithNameAndVersion7Id()
    {
        // Arrange
        const string name = "Charity Gala";

        // Act
        var @event = Event.Create(name);

        // Assert
        @event.Name.ShouldBe(name);
        @event.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Create_CalledTwice_ReturnsDistinctIds()
    {
        // Arrange
        const string name = "Charity Gala";

        // Act
        var first = Event.Create(name);
        var second = Event.Create(name);

        // Assert
        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public void Create_WithExplicitId_UsesThatId()
    {
        // Arrange
        var id = Guid.Parse("01a1ca7a-ae00-7b0f-9c06-2cc1ea1f1255");

        // Act
        var @event = Event.Create(id, "Charity Gala");

        // Assert
        @event.Id.ShouldBe(id);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        // Arrange
        var name = new string('a', Event.NameMaxLength);

        // Act
        var @event = Event.Create(name);

        // Assert
        @event.Name.Length.ShouldBe(Event.NameMaxLength);
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        string name = null!;

        // Act
        var act = () => Event.Create(name);

        // Assert
        act.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void Create_WithEmptyOrWhitespaceName_ThrowsArgumentException(string name)
    {
        // Arrange (name supplied by InlineData)

        // Act
        var act = () => Event.Create(name);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_WithNameOverMaxLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var name = new string('a', Event.NameMaxLength + 1);

        // Act
        var act = () => Event.Create(name);

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>().ParamName.ShouldBe("name");
    }
}
