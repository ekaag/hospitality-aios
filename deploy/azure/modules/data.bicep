// data: the app's relational database, Azure Database for PostgreSQL flexible server (adapter EfCore.PostgreSql). Only
// Microsoft Entra sign-in: passwords are off. Its Entra administrator is identity-data, which the migration step runs as;
// the API signs in as its own identity, whose role that step creates and grants the tables to. Reachable from Azure
// services only (the firewall's 0.0.0.0 rule), so the migration job and the API reach it and nothing outside Azure does.
// The tier is set per environment in the catalog. Like every stateful resource, teardown keeps it unless asked
// (P12); where an environment protects its stateful resources, its backups are also geo-redundant.

@description('The server name, 3 to 63 lowercase letters, digits, and hyphens, globally unique, e.g. data-dev-uksouth-psql.')
@minLength(3)
@maxLength(63)
param name string

param location string

param tags object

@description('The administrator: identity-data.')
param adminPrincipalId string

@description('The administrator\'s name: identity-data.name.')
param adminName string

@description('Whether to keep geo-redundant backups.')
param protect bool

@description('The compute size, e.g. Standard_B1ms.')
param sku string

@description('The compute tier for the size.')
@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param tier string

@description('The database the app uses.')
param database string = 'hospitalityos'

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
    tier: tier
  }
  properties: {
    version: '17'
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: protect ? 'Enabled' : 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: subscription().tenantId
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// The server accepts one change at a time, so the administrator, the firewall rule, and the database follow each other.
resource admin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2024-08-01' = {
  parent: server
  name: adminPrincipalId
  properties: {
    principalType: 'ServicePrincipal'
    principalName: adminName
    tenantId: subscription().tenantId
  }
}

resource azureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: server
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
  dependsOn: [
    admin
  ]
}

resource appDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: database
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    azureServices
  ]
}

output serverId string = server.id
@description('The host the app connects to.')
output host string = server.properties.fullyQualifiedDomainName
@description('The database the app uses.')
output database string = appDatabase.name
