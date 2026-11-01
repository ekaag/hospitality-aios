// search-content: Azure AI Search for the property's content (ISearchIndex, IVectorStore, adapter AzureAISearch). API keys
// are off: the API signs in as its identity and may read and write documents only. Indexes are not ARM resources, so the
// deployment applies their definitions (deploy/azure/search/) after this module, as whoever runs it, whom this module
// lets manage indexes. The tier is set per environment in the catalog (Free in dev: one per subscription, three indexes).

@description('The service name, 2 to 60 lowercase letters, digits, and single hyphens, globally unique, e.g. search-content-dev-uksouth-srch.')
@minLength(2)
@maxLength(60)
param name string

param location string

param tags object

@description('The principal that reads and writes documents: the API identity.')
param principalId string

@description('The principal running the deployment, which applies the index definitions.')
param deployerPrincipalId string

@description('The pricing tier.')
@allowed(['free', 'basic', 'standard'])
param sku string

resource service 'Microsoft.Search/searchServices@2023-11-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    disableLocalAuth: true
    publicNetworkAccess: 'enabled'
  }
}

// Search Index Data Contributor, and Search Service Contributor (which manages indexes).
var dataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')
var serviceContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0')

resource documents 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(service.id, principalId, dataContributor)
  scope: service
  properties: {
    roleDefinitionId: dataContributor
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource indexes 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(service.id, deployerPrincipalId, serviceContributor)
  scope: service
  properties: {
    roleDefinitionId: serviceContributor
    principalId: deployerPrincipalId
  }
}

output serviceId string = service.id
@description('The URL the app and the index step connect to.')
output endpoint string = 'https://${service.name}.search.windows.net'
