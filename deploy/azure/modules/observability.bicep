param baseName string
param location string
param tags object

resource logs 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: '${baseName}-logs'
  location: location
  tags: tags
  properties: {
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource monitor 'Microsoft.Monitor/accounts@2025-10-03' = {
  name: '${baseName}-monitor'
  location: location
  tags: tags
  properties: {
    publicNetworkAccess: 'Enabled'
  }
}

resource grafana 'Microsoft.Dashboard/grafana@2024-10-01' = {
  name: '${baseName}-grafana'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'Standard'
  }
  properties: {
    apiKey: 'Disabled'
    deterministicOutboundIP: 'Enabled'
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: 'Enabled'
  }
}

output logAnalyticsWorkspaceId string = logs.id
output monitorWorkspaceId string = monitor.id
output grafanaId string = grafana.id
output grafanaEndpoint string = grafana.properties.endpoint
