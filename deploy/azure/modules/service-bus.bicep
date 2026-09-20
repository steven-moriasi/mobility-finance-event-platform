@minLength(6)
param baseName string
param location string
param workloadPrincipalId string
param tags object

resource serviceBus 'Microsoft.ServiceBus/namespaces@2026-01-01' = {
  name: '${baseName}-sb'
  location: location
  tags: tags
  sku: {
    name: 'Premium'
    tier: 'Premium'
    capacity: 1
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    premiumMessagingPartitions: 1
    publicNetworkAccess: 'Enabled'
    zoneRedundant: true
  }
}

resource events 'Microsoft.ServiceBus/namespaces/topics@2026-01-01' = {
  name: 'mobility-events'
  parent: serviceBus
  properties: {
    defaultMessageTimeToLive: 'P14D'
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    enableBatchedOperations: true
    enableExpress: false
    enablePartitioning: false
    maxMessageSizeInKilobytes: 1024
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: true
    supportOrdering: true
  }
}

resource activationSubscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2026-01-01' = {
  name: 'activation-workflows'
  parent: events
  properties: {
    deadLetteringOnFilterEvaluationExceptions: true
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P14D'
    enableBatchedOperations: true
    lockDuration: 'PT1M'
    maxDeliveryCount: 8
    requiresSession: true
  }
}

resource senderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBus.id, workloadPrincipalId, 'sender')
  scope: serviceBus
  properties: {
    principalId: workloadPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
    )
  }
}

resource receiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBus.id, workloadPrincipalId, 'receiver')
  scope: serviceBus
  properties: {
    principalId: workloadPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4f6c79d8-8b36-4e0d-bc4d-58e515142649'
    )
  }
}

output namespaceId string = serviceBus.id
output fullyQualifiedNamespace string = '${serviceBus.name}.servicebus.windows.net'
output topicName string = events.name
