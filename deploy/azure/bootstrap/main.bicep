// The Azure bootstrap: the identity GitHub Actions signs in as (OIDC, no stored credentials) and what it may do. CI
// cannot create the identity it signs in with, so this is the one deployment run outside CI, by the orchestrator's
// `bootstrap` command with an administrator's sign-in; `bootstrap-down` removes it. It is a subscription deployment
// stack, so running it again changes nothing and deleting the stack removes everything it made.

targetScope = 'subscription'

@description('The resource group for the deployment identity, shared by every environment: rg-hospitalityos-deploy.')
param groupName string

@description('The identity name, e.g. deploy-shared-uksouth-id.')
param identityName string

param location string

param tags object

@description('The GitHub repository allowed to sign in, owner/name.')
param repository string

@description('The GitHub environments whose jobs may deploy, e.g. dev.')
param environments array

resource group 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: groupName
  location: location
  tags: tags
}

module identity 'identity.bicep' = {
  name: 'deploy-identity'
  scope: group
  params: {
    name: identityName
    location: location
    tags: tags
    repository: repository
    environments: environments
  }
}

// Contributor: creates and changes the plan's resources.
var contributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

// Role Based Access Control Administrator, limited by the condition to the roles the modules grant: AcrPull, Key Vault
// Secrets User, Azure Service Bus Data Sender and Data Receiver, and Storage Blob Data Contributor. CI can let the API
// identity pull images, read secrets, send and receive messages, and read and write blobs, and nothing more.
var rbacAdministrator = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f58310d9-a9f6-439a-9e8d-f62e7b41a168')
var grantable = '7f951dda-4ed3-4680-a7ca-43fe172d538d, 4633458b-17de-408a-b874-0445c86b69e6, 69a216fc-b8fb-44d8-bc22-1f3c2cd27a39, 4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0, ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var onlyGrantable = '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${grantable}})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${grantable}}))'

resource contribute 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, identityName, contributor)
  properties: {
    roleDefinitionId: contributor
    principalId: identity.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource grant 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, identityName, rbacAdministrator)
  properties: {
    roleDefinitionId: rbacAdministrator
    principalId: identity.outputs.principalId
    principalType: 'ServicePrincipal'
    condition: onlyGrantable
    conditionVersion: '2.0'
  }
}

output clientId string = identity.outputs.clientId
output tenantId string = tenant().tenantId
output subscriptionId string = subscription().subscriptionId
