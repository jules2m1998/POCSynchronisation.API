using Poc.Synchronisation.Domain.Abstractions;

namespace POCSync.MAUI;

public class PlatformIdentifier : IPlatformIdentifier
{
    public Poc.Synchronisation.Domain.Abstractions.Platform GetPlatform()
    {
        return Poc.Synchronisation.Domain.Abstractions.Platform.Android;
    }
}
