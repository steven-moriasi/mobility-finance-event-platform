param baseName string
param location string
param subnetId string
param logAnalyticsWorkspaceId string
param monitorWorkspaceId string
param registryName string
param privateCluster bool
param tags object

resource cluster 'Microsoft.ContainerService/managedClusters@2025-01-01' = {
  name: '${baseName}-aks'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    aadProfile: {
      enableAzureRBAC: true
      managed: true
    }
    addonProfiles: {
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
      omsagent: {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalyticsWorkspaceId
          useAADAuth: 'true'
        }
      }
    }
    agentPoolProfiles: [
      {
        name: 'system'
        count: 3
        enableAutoScaling: true
        maxCount: 6
        minCount: 3
        mode: 'System'
        osDiskSizeGB: 128
        osDiskType: 'Managed'
        osSKU: 'AzureLinux'
        osType: 'Linux'
        type: 'VirtualMachineScaleSets'
        vmSize: 'Standard_D4ds_v5'
        vnetSubnetID: subnetId
        availabilityZones: [
          '1'
          '2'
          '3'
        ]
      }
    ]
    azureMonitorProfile: {
      metrics: {
        enabled: true
        kubeStateMetrics: {
          metricAnnotationsAllowList: ''
          metricLabelsAllowlist: ''
        }
      }
    }
    autoUpgradeProfile: {
      nodeOSUpgradeChannel: 'NodeImage'
      upgradeChannel: 'stable'
    }
    dnsPrefix: baseName
    enableRBAC: true
    networkProfile: {
      networkPlugin: 'azure'
      networkPluginMode: 'overlay'
      networkPolicy: 'cilium'
      networkDataplane: 'cilium'
      outboundType: 'loadBalancer'
      serviceCidr: '10.41.0.0/16'
      dnsServiceIP: '10.41.0.10'
    }
    oidcIssuerProfile: {
      enabled: true
    }
    securityProfile: {
      defender: {
        logAnalyticsWorkspaceResourceId: logAnalyticsWorkspaceId
        securityMonitoring: {
          enabled: true
        }
      }
      imageCleaner: {
        enabled: true
        intervalHours: 48
      }
      workloadIdentity: {
        enabled: true
      }
    }
    apiServerAccessProfile: {
      enablePrivateCluster: privateCluster
      enablePrivateClusterPublicFQDN: false
      privateDNSZone: privateCluster ? 'system' : ''
    }
  }
}

resource metricsRuleGroup 'Microsoft.AlertsManagement/prometheusRuleGroups@2023-03-01' = {
  name: '${baseName}-platform-rules'
  location: location
  tags: tags
  properties: {
    clusterName: cluster.name
    enabled: true
    interval: 'PT1M'
    scopes: [
      monitorWorkspaceId
    ]
    rules: [
      {
        alert: 'MobilityFinanceDeploymentUnavailable'
        annotations: {
          description: 'A Mobility Finance deployment has unavailable replicas.'
          summary: 'Mobility Finance deployment is unavailable.'
        }
        enabled: true
        expression: 'kube_deployment_status_replicas_unavailable{namespace="mobility-finance"} > 0'
        for: 'PT5M'
        labels: {
          severity: 'warning'
        }
        severity: 2
      }
    ]
  }
}

resource registry 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: registryName
}

resource registryPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, cluster.id, 'pull')
  scope: registry
  properties: {
    principalId: cluster.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '7f951dda-4ed3-4680-a7ca-43fe172d538d'
    )
  }
}

output clusterId string = cluster.id
output clusterName string = cluster.name
output kubeletIdentityObjectId string = cluster.properties.identityProfile.kubeletidentity.objectId
output oidcIssuerUrl string = cluster.properties.oidcIssuerProfile.issuerURL
