using Astrea.Application.Abstractions.Clock;

namespace Astrea.Infrastructure.Clock;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
