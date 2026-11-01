// identity-data: the database's administrator. Only the migration step signs in as it, to change the schema and grant
// the API's identity the tables; the API itself never can (P12).

@description('The identity name, e.g. identity-data-dev-uksouth-id.')
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
