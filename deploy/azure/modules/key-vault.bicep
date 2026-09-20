param baseName string
param location string
param tenantId string
param workloadPrincipalId string
param tags object

resource keyVault 'Microsoft.KeyVault/vaults@2024-11-01' = {
  name: take('${baseName}-kv', 24)
  location: location
  tags: tags
  properties: {
    accessPolicies: []
    enablePurgeProtection: true
    enableRbacAuthorization: true
    enableSoftDelete: true
    publicNetworkAccess: 'Enabled'
    sku: {
      family: 'A'
      name: 'standard'
    }
    softDeleteRetentionInDays: 90
    tenantId: tenantId
  }
}

resource secretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, workloadPrincipalId, 'secrets-user')
  scope: keyVault
  properties: {
    principalId: workloadPrincipalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'
    )
  }
}

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output vaultUri string = keyVault.properties.vaultUri
