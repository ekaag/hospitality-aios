// secrets: the Key Vault the app reads secrets from by reference, with RBAC rather than access policies; the API's
// identity may read secrets. Purge protection is on wherever an environment protects its stateful resources (P12).

@description('The vault name, at most 24 characters, e.g. secrets-dev-uksouth-kv.')
@maxLength(24)
param name string

param location string

param tags object

@description('The principal allowed to read secrets: the API identity.')
param readerPrincipalId string

@description('Whether a deleted vault can never be purged, only recovered.')
param protect bool

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    // Purge protection cannot be switched off once on, so it is only sent when asked for.
    enablePurgeProtection: protect ? true : null
    publicNetworkAccess: 'Enabled'
  }
}

// Key Vault Secrets User.
var secretsUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')

resource read 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, readerPrincipalId, secretsUser)
  scope: vault
  properties: {
    roleDefinitionId: secretsUser
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output vaultId string = vault.id
output vaultUri string = vault.properties.vaultUri
output vaultName string = vault.name
