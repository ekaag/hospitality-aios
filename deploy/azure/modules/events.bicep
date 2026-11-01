// events: the Service Bus namespace for events between parts of the app and for scheduled work (IEventTransport,
// IScheduler, adapter AzureServiceBus). The topic, one subscription per event handler, and the scheduled-work queue come
// from deploy/events.json, which a test keeps equal to the API's handlers. Topics need the Standard tier. SAS keys are
// off: the API signs in as its identity and may send and receive, but not manage entities.

@description('The namespace name, 6 to 50 characters and globally unique, e.g. events-dev-uksouth-sbns.')
@minLength(6)
@maxLength(50)
param name string

param location string

param tags object

@description('The principal that sends and receives: the API identity.')
param principalId string

var events = loadJsonContent('../../events.json')

// Events nobody has taken within two weeks move to the dead letters rather than vanishing.
var timeToLive = 'P14D'

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespace
  name: events.topic
  properties: {
    defaultMessageTimeToLive: timeToLive
    requiresDuplicateDetection: false
  }
}

resource subscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = [
  for subscription in events.subscriptions: {
    parent: topic
    name: subscription
    properties: {
      // The app dead-letters on its last delivery itself, keeping the handler's error; this is the broker's backstop.
      maxDeliveryCount: events.maxDeliveryCount
      lockDuration: 'PT1M'
      defaultMessageTimeToLive: timeToLive
      deadLetteringOnMessageExpiration: true
    }
  }
]

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: namespace
  name: events.queue
  properties: {
    maxDeliveryCount: events.maxDeliveryCount
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
  }
}

// Azure Service Bus Data Sender and Data Receiver.
var dataSender = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
var dataReceiver = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')

resource send 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, principalId, dataSender)
  scope: namespace
  properties: {
    roleDefinitionId: dataSender
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

resource receive 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(namespace.id, principalId, dataReceiver)
  scope: namespace
  properties: {
    roleDefinitionId: dataReceiver
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

output namespaceId string = namespace.id
@description('The fully qualified namespace the app connects to, e.g. events-dev-uksouth-sbns.servicebus.windows.net.')
output namespaceHost string = split(split(namespace.properties.serviceBusEndpoint, '//')[1], ':')[0]
output topic string = topic.name
output queue string = queue.name
output maxDeliveryCount int = events.maxDeliveryCount
