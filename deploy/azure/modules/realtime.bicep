// realtime: Azure SignalR Service carrying the API hub's connections (IRealtimeNotifier, adapter AzureSignalR), so any
// number of API replicas reach every connected staff member and guest. Default mode: the hub stays in the API, clients
// negotiate with the API and are sent on to the service. Access keys are off: the API connects as its identity. The tier is
// set per environment in the catalog (Free_F1 in dev: 20 connections, 20,000 messages a day).

@description('The service name, 3 to 63 letters, digits, and hyphens, globally unique, e.g. realtime-dev-uksouth-sigr.')
@minLength(3)
@maxLength(63)
param name string

param location string

param tags object

@description('The principal that serves the hub through the service: the API identity.')
param principalId string

@description('The pricing tier.')
@allowed(['Free_F1', 'Standard_S1', 'Premium_P1'])
param sku string

resource service 'Microsoft.SignalRService/signalR@2024-03-01' = {
  name: name
  location: location
  tags: tags
  kind: 'SignalR'
  sku: {
    name: sku
    capacity: 1
  }
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
    ]
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

// SignalR App Server.
var appServer = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '420fcaa2-552c-430f-98ca-3264be4806c7')

resource serve 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(service.id, principalId, appServer)
  scope: service
  properties: {
    roleDefinitionId: appServer
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output serviceId string = service.id
@description('The service URL the API connects to.')
output endpoint string = 'https://${service.properties.hostName}'
