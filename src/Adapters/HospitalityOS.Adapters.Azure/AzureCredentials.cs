using Azure.Core;
using Azure.Identity;

namespace HospitalityOS.Adapters.Azure;

/// <summary>
/// The app's own identity, shared by every Azure adapter so tokens are cached once. In Azure it is the API's
/// user-assigned managed identity, which <c>AZURE_CLIENT_ID</c> names (api.bicep); locally, whoever is signed in to the
/// Azure CLI.
/// </summary>
internal static class AzureCredentials
{
    private static readonly Lazy<TokenCredential> app = new(() => new DefaultAzureCredential());

    public static TokenCredential App => app.Value;
}
