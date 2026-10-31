// apps: the Container Apps environment the app's hosts run in, sending their logs to the logs workspace.

@description('The environment name, e.g. apps-dev-uksouth-cae.')
param name string

param location string

param tags object

@description('The Log Analytics workspace: logs.workspaceId.')
param logsWorkspaceId string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: last(split(logsWorkspaceId, '/'))
  scope: resourceGroup(split(logsWorkspaceId, '/')[2], split(logsWorkspaceId, '/')[4])
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

output environmentId string = environment.id
output defaultDomain string = environment.properties.defaultDomain
