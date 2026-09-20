targetScope = 'resourceGroup'

@description('Azure region for the platform resources.')
param location string = resourceGroup().location

@description('Short environment identifier.')
@allowed([
  'dev'
  'test'
  'prod'
])
param environmentName string

@description('Microsoft Entra tenant used by AKS workload identity.')
param tenantId string = tenant().tenantId

@description('Kubernetes namespace hosting the platform.')
param kubernetesNamespace string = 'mobility-finance'

@description('Kubernetes service account used by the platform.')
param kubernetesServiceAccount string = 'mobility-finance'

@description('Object ID of the Microsoft Entra administrator for PostgreSQL.')
param postgresAdministratorObjectId string

@description('Display name of the Microsoft Entra administrator for PostgreSQL.')
param postgresAdministratorName string

@description('Deploy a private AKS control plane.')
param privateCluster bool = true

var suffix = take(uniqueString(subscription().subscriptionId, resourceGroup().id), 8)
var baseName = 'mf-${environmentName}-${suffix}'
var tags = {
  application: 'mobility-finance'
  environment: environmentName
  managedBy: 'bicep'
  dataClassification: 'synthetic'
}

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

module registry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

module identity 'modules/workload-identity.bicep' = {
  name: 'workload-identity'
  params: {
    baseName: baseName
    location: location
    oidcIssuerUrl: cluster.outputs.oidcIssuerUrl
    kubernetesNamespace: kubernetesNamespace
    kubernetesServiceAccount: kubernetesServiceAccount
    tags: tags
  }
}

module messaging 'modules/service-bus.bicep' = {
  name: 'service-bus'
  params: {
    baseName: baseName
    location: location
    workloadPrincipalId: identity.outputs.principalId
    tags: tags
  }
}

module secrets 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    baseName: baseName
    location: location
    tenantId: tenantId
    workloadPrincipalId: identity.outputs.principalId
    tags: tags
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    baseName: baseName
    location: location
    tags: tags
  }
}

module database 'modules/postgresql.bicep' = {
  name: 'postgresql'
  params: {
    baseName: baseName
    location: location
    subnetId: network.outputs.postgresSubnetId
    virtualNetworkId: network.outputs.virtualNetworkId
    administratorObjectId: postgresAdministratorObjectId
    administratorName: postgresAdministratorName
    tenantId: tenantId
    tags: tags
  }
}

module cluster 'modules/aks.bicep' = {
  name: 'aks'
  params: {
    baseName: baseName
    location: location
    subnetId: network.outputs.aksSubnetId
    logAnalyticsWorkspaceId: observability.outputs.logAnalyticsWorkspaceId
    monitorWorkspaceId: observability.outputs.monitorWorkspaceId
    registryName: registry.outputs.registryName
    privateCluster: privateCluster
    tags: tags
  }
}

output clusterName string = cluster.outputs.clusterName
output containerRegistryName string = registry.outputs.registryName
output grafanaEndpoint string = observability.outputs.grafanaEndpoint
output keyVaultName string = secrets.outputs.keyVaultName
output postgresHost string = database.outputs.fullyQualifiedDomainName
output serviceBusNamespace string = messaging.outputs.fullyQualifiedNamespace
output workloadIdentityClientId string = identity.outputs.clientId
