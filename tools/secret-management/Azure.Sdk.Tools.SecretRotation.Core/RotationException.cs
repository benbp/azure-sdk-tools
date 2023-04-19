namespace Azure.Sdk.Tools.SecretManagement.Core;

public class RotationException : Exception
{
    public RotationException(string message, Exception? innerException = default) : base(message, innerException)
    {
    }
}
