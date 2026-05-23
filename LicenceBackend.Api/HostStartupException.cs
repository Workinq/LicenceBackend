namespace LicenceBackend.Api;

public sealed class HostStartupException : Exception
{
    public HostStartupException(string message, Exception inner) : base(message, inner) { }
}
