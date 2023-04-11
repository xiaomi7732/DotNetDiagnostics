using Azure.Core;
using Azure.Identity;
using DotNet.Diagnostics.Core;

namespace DotNet.Diagnostics.JobDispatchers.AzureBlob;

public class TokenCredentialBase<TService, TOptions> : TokenCredential<TService>
    where TService : class
    where TOptions : TokenAuthOptions
{
    private readonly TokenAuthOptions _options;
    private TokenCredential _tokenCredential;

    public TokenCredentialBase(TokenAuthOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
        {
            ManagedIdentityClientId = _options.ManagedIdentityClientId,
            ExcludeInteractiveBrowserCredential = true,
        };
        _tokenCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => _tokenCredential.GetToken(requestContext, cancellationToken);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => _tokenCredential.GetTokenAsync(requestContext, cancellationToken);
}