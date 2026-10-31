// logs: the Log Analytics workspace every resource sends its logs and metrics to.
// The name, location, and tags come from the orchestrator (deploy/tooling), which derives them from the plan.

@description('The workspace name, e.g. logs-dev-uksouth-log.')
param name string

param location string

param tags object

@description('Days logs are kept.')
@minValue(30)
param retentionInDays int = 30

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
  }
}

output workspaceId string = workspace.id
