namespace RestaurantHandler.IntegrationTests;

/// <summary>Horloge réglable dans les deux sens (chaque test repart du même instant).</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void SetUtcNow(DateTimeOffset value) => _now = value;

    public void Advance(TimeSpan delta) => _now += delta;
}
