namespace Azure.Sdk.Tools.SecretManagement.Core;

public class TimeProvider
{
    public virtual DateTimeOffset GetCurrentDateTimeOffset()
    {
        return DateTimeOffset.UtcNow;
    }
}
