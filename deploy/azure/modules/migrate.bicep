// migrate: the deployment's migration step, a Container Apps job the orchestrator starts before each API deployment and
// waits for. It runs the API image in its migrate mode with the API's own app settings, signed in to the database as
// identity-data (its administrator): it applies the schema, creates and grants the API identity's role, seeds where the
// environment asks for it, and exits. The image is pulled as the API identity, which the registry already trusts.

@description('The job name, at most 32 characters, e.g. migrate-dev-uksouth-caj.')
@maxLength(32)
param name string

param location string

param tags object

@description('The Container Apps environment: apps.environmentId.')
param environmentId string

@description('The registry login server: images.loginServer.')
param registryServer string

@description('The identity the image is pulled with: identity-api.identityId.')
param pullIdentityId string

@description('The identity the job signs in to the database as: identity-data.identityId.')
param identityId string

@description('Its client ID: identity-data.clientId.')
param identityClientId string

@description('Its name, which is its database role: identity-data.name.')
param identityName string

@description('The API identity\'s name, the role the step creates and grants: identity-api.name.')
param appRoleName string

@description('The API identity\'s object ID: identity-api.principalId.')
param appRoleObjectId string

@description('The database host: data.host.')
param dataHost string

@description('The database: data.database.')
param database string

@description('Whether to seed the Aurora Grand reference property.')
param seed bool

@description('The image as repository:tag, tagged by commit SHA, never latest.')
param image string

@description('The API\'s app settings, as environment variables: name to value.')
param appSettings object = {}

// The API's settings, signed in as the administrator instead, with the role to grant and whether to seed.
var settings = union(
  appSettings,
  {
    ASPNETCORE_ENVIRONMENT: 'Production'
    AZURE_CLIENT_ID: identityClientId
    Persistence__Provider: 'PostgreSql'
    Persistence__SignIn: 'Entra'
    Persistence__ConnectionString: 'Host=${dataHost};Database=${database};Username=${identityName};Ssl Mode=Require'
    Persistence__AppRole__Name: appRoleName
    Persistence__AppRole__ObjectId: appRoleObjectId
    Seed__AuroraGrand__Enabled: string(seed)
    // Its output is in the job's console logs; identity-data may not send telemetry.
    Telemetry__Exporter: 'None'
  }
)

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
      '${identityId}': {}
    }
  }
  properties: {
    environmentId: environmentId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registryServer
          identity: pullIdentityId
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: '${registryServer}/${image}'
          args: [
            'migrate'
          ]
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
        }
      ]
    }
  }
}

output jobId string = job.id
@description('The job name, which the orchestrator starts.')
output jobName string = job.name
@description('The image the job runs, so drift detection checks the rest against it.')
output image string = image
