// blobs: the storage account for files the app keeps (IBlobStore, adapter AzureBlobStorage): evidence photos now,
// content assets later, in one private container. Shared keys are off, so the API signs in as its identity, which may
// read and write blobs but not manage the account. Deleted blobs and containers can be restored for a while, longer
// wherever an environment protects its stateful resources (P12).

@description('The account name, 3 to 24 lowercase letters and digits, globally unique, e.g. blobsdevuksouthst.')
@minLength(3)
@maxLength(24)
param name string

param location string

param tags object

@description('The principal that reads and writes blobs: the API identity.')
param principalId string

@description('Whether deleted blobs are kept longer for recovery.')
param protect bool

var container = 'blobs'
var retentionDays = protect ? 30 : 7

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    publicNetworkAccess: 'Enabled'
  }
}

resource service 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: retentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: retentionDays
    }
  }
}

resource files 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: service
  name: container
  properties: {
    publicAccess: 'None'
  }
}

// Storage Blob Data Contributor.
var blobContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource readWrite 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, blobContributor)
  scope: account
  properties: {
    roleDefinitionId: blobContributor
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output accountId string = account.id
@description('The blob endpoint the app connects to, e.g. https://blobsdevuksouthst.blob.core.windows.net/.')
output serviceUri string = account.properties.primaryEndpoints.blob
output container string = files.name
