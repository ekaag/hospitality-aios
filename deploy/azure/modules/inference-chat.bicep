// inference-chat: an Azure OpenAI resource in Microsoft Foundry (kind AIServices) holding the chat model the API's
// chat, classification, extraction, and judging tasks fall back to, or use when no Anthropic key is set (adapter
// AzureOpenAI). Keys are off: the API calls as its identity. Global Standard: prompts may be processed in any region the
// model is deployed in. The deployment is named after the model and never upgraded automatically, so a task's model
// changes only with this module. Capacity is set per environment in the catalog.

@description('The resource name, 2 to 64 letters, digits, and hyphens, globally unique (also the subdomain of its endpoint), e.g. inference-chat-dev-uksouth-oai.')
@minLength(2)
@maxLength(64)
param name string

param location string

param tags object

@description('The principal that calls the models: the API identity.')
param principalId string

@description('Thousands of tokens a minute the deployment may use.')
@minValue(1)
param capacity int

@description('The chat model.')
param model string = 'gpt-5-mini'

@description('The version of the chat model.')
param version string = '2025-08-07'

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: name
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: name
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource deployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: model
  sku: {
    name: 'GlobalStandard'
    capacity: capacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: model
      version: version
    }
    versionUpgradeOption: 'NoAutoUpgrade'
  }
}

// Cognitive Services OpenAI User: may call the deployments, not change them.
var openAIUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')

resource use 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, openAIUser)
  scope: account
  properties: {
    roleDefinitionId: openAIUser
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output accountId string = account.id
@description('The https URL of the resource, which the AzureOpenAI adapter calls the v1 API under.')
output endpoint string = 'https://${name}.openai.azure.com/'
@description('The name of the deployment, which inference tasks name as their model.')
output chatDeployment string = deployment.name
