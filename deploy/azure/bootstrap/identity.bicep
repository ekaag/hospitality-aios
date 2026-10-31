// The deployment identity and the GitHub sign-ins it trusts: pushes to main, jobs in each deployment environment, and
// pull requests (for what-if previews; their role assignments still decide what they can change).

@description('The identity name, e.g. deploy-shared-uksouth-id.')
param name string

param location string

param tags object

@description('The GitHub repository allowed to sign in, owner/name.')
param repository string

@description('The GitHub environments whose jobs may deploy, e.g. dev.')
param environments array

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

var subjects = concat(
  [
    'repo:${repository}:ref:refs/heads/main'
    'repo:${repository}:pull_request'
  ],
  map(environments, environment => 'repo:${repository}:environment:${environment}')
)

// Federated credentials on one identity must be created one at a time.
@batchSize(1)
resource trust 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = [
  for subject in subjects: {
    parent: identity
    name: 'github-${uniqueString(subject)}'
    properties: {
      issuer: 'https://token.actions.githubusercontent.com'
      subject: subject
      audiences: [
        'api://AzureADTokenExchange'
      ]
    }
  }
]

output principalId string = identity.properties.principalId
output clientId string = identity.properties.clientId
