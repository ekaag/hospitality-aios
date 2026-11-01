// secrets: the Key Vault the app reads secrets from by reference, with RBAC rather than access policies; the API's
// identity may read secrets. It also holds the RSA key the API signs guests' stay tokens with (K4c), which the API may
// only sign and verify with: the private key never leaves the vault. Purge protection is on wherever an environment
// protects its stateful resources (P12).

@description('The vault name, at most 24 characters, e.g. secrets-dev-uksouth-kv.')
@maxLength(24)
param name string

param location string

param tags object

@description('The principal allowed to read secrets: the API identity.')
param readerPrincipalId string

@description('Whether a deleted vault can never be purged, only recovered.')
param protect bool

@description('The stay token signing key\'s name.')
param signingKeyName string = 'stay-tokens'

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

// Rotated every 90 days; tokens signed by the old version stop validating soon after, and guest apps open their sessions
// again from the same check-in link without the guest noticing.
resource signingKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: vault
  name: signingKeyName
  tags: tags
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'sign'
      'verify'
    ]
    rotationPolicy: {
      attributes: {
        expiryTime: 'P1Y'
      }
      lifetimeActions: [
        {
          action: {
            type: 'rotate'
          }
          trigger: {
            timeAfterCreate: 'P90D'
          }
        }
      ]
    }
  }
}

// Key Vault Crypto User, on the signing key only.
var cryptoUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '12338af0-0e69-4776-bea7-57ae8d297424')

resource sign 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(signingKey.id, readerPrincipalId, cryptoUser)
  scope: signingKey
  properties: {
    roleDefinitionId: cryptoUser
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output vaultId string = vault.id
output vaultUri string = vault.properties.vaultUri
output vaultName string = vault.name
output signingKeyName string = signingKey.name
