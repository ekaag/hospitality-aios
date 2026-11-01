// An environment's Microsoft Entra External ID tenant, where staff sign in (K4b), linked to the subscription for billing.
// Created once by the orchestrator's `bootstrap-signin` command with an administrator's sign-in, since what lives inside
// the new tenant can only be set up by someone signed in to it; the signin resource then deploys its apps. Creating a
// tenant can take up to 30 minutes. The resource type is still in preview.

@description('The tenant\'s domain prefix: <prefix>.onmicrosoft.com and <prefix>.ciamlogin.com. Unique across Entra.')
param domainPrefix string

@description('Where the tenant\'s data is kept, as a country code, e.g. GB.')
param countryCode string

@description('The tenant\'s data location: United States, Europe, Asia Pacific, or Australia.')
param location string

param tags object

resource directory 'Microsoft.AzureActiveDirectory/ciamDirectories@2023-05-17-preview' = {
  name: '${domainPrefix}.onmicrosoft.com'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'A0'
  }
  properties: {
    createTenantProperties: {
      countryCode: countryCode
      displayName: 'HospitalityOS staff (${domainPrefix})'
    }
  }
}

output tenantId string = directory.properties.tenantId
output domainName string = directory.properties.domainName
