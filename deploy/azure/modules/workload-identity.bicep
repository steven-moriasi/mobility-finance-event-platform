param baseName string
param location string
param oidcIssuerUrl string
param kubernetesNamespace string
param kubernetesServiceAccount string
param tags object

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${baseName}-workload'
  location: location
  tags: tags
}

resource federatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2024-11-30' = {
  name: 'mobility-finance'
  parent: identity
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: oidcIssuerUrl
    subject: 'system:serviceaccount:${kubernetesNamespace}:${kubernetesServiceAccount}'
  }
}

output clientId string = identity.properties.clientId
output principalId string = identity.properties.principalId
