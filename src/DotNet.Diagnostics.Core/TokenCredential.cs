using Azure.Core;

namespace DotNet.Diagnostics.Core;

public abstract class TokenCredential<TService> : TokenCredential
    where TService : class
{
}