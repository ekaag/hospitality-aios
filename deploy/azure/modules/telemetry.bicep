// telemetry: Application Insights over the logs workspace, receiving the API's OpenTelemetry traces, metrics, and logs
// (the API's Telemetry:Exporter AzureMonitor). Local auth is off, so its connection string alone can send nothing: the
// API sends as its identity, which may publish telemetry and nothing more.

@description('The component name, e.g. telemetry-dev-uksouth-appi.')
@maxLength(255)
param name string

param location string

param tags object

@description('The Log Analytics workspace it stores in: logs.workspaceId.')
param workspaceId string

@description('The principal that sends telemetry: the API identity.')
param principalId string

resource component 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspaceId
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// Monitoring Metrics Publisher: may send telemetry, not read it.
var metricsPublisher = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')

resource publish 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(component.id, principalId, metricsPublisher)
  scope: component
  properties: {
    roleDefinitionId: metricsPublisher
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output componentId string = component.id
@description('Where the API sends telemetry; not a secret, since local auth is off.')
output connectionString string = component.properties.ConnectionString
