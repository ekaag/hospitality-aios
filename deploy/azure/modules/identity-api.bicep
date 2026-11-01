// identity-api: the API's own identity. The registry, the vault, the database, and the other services trust it, so the
// API holds no credentials.

@description('The identity name, e.g. identity-api-dev-uksouth-id.')
param name string

param location string

param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

output identityId string = identity.id
output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
@description('The identity name, which is also its database role.')
output name string = identity.name
