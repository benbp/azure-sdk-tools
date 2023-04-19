namespace Azure.Sdk.Tools.SecretManagement.Configuration;

public class RotationConfigurationException : Exception
{
    public RotationConfigurationException(string message, Exception? innerException = default) : base(message,
        innerException)
    {
    }
}
