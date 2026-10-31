// images: the container registry the app's images are pushed to (tagged by commit SHA) and pulled from by the API's
// identity. No admin user: pushes and pulls are by identity only.

@description('The registry name. Registries allow no delimiters, e.g. imagesdevuksouthacr.')
param name string

param location string

param tags object

@description('The principal allowed to pull images: the API identity.')
param pullPrincipalId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

// AcrPull.
var acrPull = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource pull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, pullPrincipalId, acrPull)
  scope: registry
  properties: {
    roleDefinitionId: acrPull
    principalId: pullPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output registryId string = registry.id
output loginServer string = registry.properties.loginServer
output registryName string = registry.name
