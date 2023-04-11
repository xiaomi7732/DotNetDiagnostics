namespace DotNet.Diagnostics.Core;

public abstract class TokenAuthOptions
{
    /// <summary>
    /// Gets or sets the client id for managed identity.
    /// </summary>
    public string? ManagedIdentityClientId { get; set; }
}