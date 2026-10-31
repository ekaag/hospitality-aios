// api: the API host as a container app, pulling its image by identity and answering /health. While persistence and
// eventing are in memory (until K1 and K2), it must run as exactly one replica, since that state is per process.

@description('The container app name, at most 32 characters, e.g. api-dev-uksouth-ca.')
@maxLength(32)
param name string

param location string

param tags object

@description('The Container Apps environment: apps.environmentId.')
param environmentId string

@description('The registry login server: images.loginServer.')
param registryServer string

@description('The API identity, used to pull the image: identity-api.identityId.')
param identityId string

@description('The API identity client ID, for the app to sign in as it: identity-api.clientId.')
param identityClientId string

@description('The vault the app reads secrets from: secrets.vaultUri.')
param vaultUri string

@description('The image as repository:tag, tagged by commit SHA, never latest.')
param image string

@description('App settings for the plan bindings, as environment variables: name to value.')
param appSettings object = {}

var settings = union(
  {
    ASPNETCORE_ENVIRONMENT: 'Production'
    AZURE_CLIENT_ID: identityClientId
    HOSPITALITYOS_VAULT_URI: vaultUri
  },
  appSettings
)

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registryServer
          identity: identityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: '${registryServer}/${image}'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            for setting in items(settings): {
              name: setting.key
              value: string(setting.value)
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

@description('The image running, so drift detection checks the rest against it.')
output image string = image
output fqdn string = app.properties.configuration.ingress.fqdn
output url string = 'https://${app.properties.configuration.ingress.fqdn}'
