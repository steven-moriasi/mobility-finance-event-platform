param baseName string
param location string
param tags object

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2025-09-01' = {
  name: '${baseName}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.40.0.0/16'
      ]
    }
  }
}

resource aksSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-09-01' = {
  name: 'aks'
  parent: virtualNetwork
  properties: {
    addressPrefix: '10.40.0.0/20'
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

resource postgresSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-09-01' = {
  name: 'postgres'
  parent: virtualNetwork
  properties: {
    addressPrefix: '10.40.16.0/24'
    delegations: [
      {
        name: 'postgres-flexible-server'
        properties: {
          serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers'
        }
      }
    ]
  }
}

resource privateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2025-09-01' = {
  name: 'private-endpoints'
  parent: virtualNetwork
  properties: {
    addressPrefix: '10.40.17.0/24'
    privateEndpointNetworkPolicies: 'Disabled'
  }
}

output virtualNetworkId string = virtualNetwork.id
output aksSubnetId string = aksSubnet.id
output postgresSubnetId string = postgresSubnet.id
output privateEndpointSubnetId string = privateEndpointSubnet.id
