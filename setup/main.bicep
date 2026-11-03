targetScope = 'resourceGroup'

@description('Region supporting Foundry Memory and both selected models. Memory is currently in preview.')
param location string = 'eastus2'

@description('Globally unique Foundry account name, also used as its custom subdomain.')
@minLength(2)
@maxLength(64)
param foundryAccountName string = 'memory-${uniqueString(resourceGroup().id)}'

@description('Name of the Foundry project.')
param projectName string = 'memory-project'

@description('Object ID of the developer or application that runs the memory sample (not an application/client ID).')
@minLength(1)
param principalId string

@description('Principal type of the identity running the sample.')
@allowed([
  'User'
  'ServicePrincipal'
  'Group'
])
param principalType string = 'User'

@description('Azure OpenAI chat model selected in setup.azcli.')
param chatModelName string

@description('Version of the selected chat model.')
param chatModelVersion string

@description('Chat model deployment name consumed by Program.cs.')
param chatModelDeploymentName string = chatModelName

param chatModelSku string = 'GlobalStandard'

@description('Chat model capacity in deployment units; availability depends on subscription quota.')
@minValue(1)
param chatModelCapacity int = 10

@description('Azure OpenAI embedding model selected in setup.azcli.')
param embeddingModelName string

@description('Version of the selected embedding model.')
param embeddingModelVersion string

@description('Embedding model deployment name consumed by Program.cs.')
param embeddingModelDeploymentName string = embeddingModelName

param embeddingModelSku string = 'Standard'

@description('Embedding model capacity in deployment units; availability depends on subscription quota.')
@minValue(1)
param embeddingModelCapacity int = 10

var foundryUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '53ca6127-db72-4b80-b1b0-d745d6d5456d'
)

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: foundryAccountName
  location: location
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    allowProjectManagement: true
    customSubDomainName: foundryAccountName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: projectName
    description: 'Foundry project for the .NET Memory Store API sample.'
  }
}

resource chatDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: chatModelDeploymentName
  sku: {
    name: chatModelSku
    capacity: chatModelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: chatModelName
      version: chatModelVersion
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: account
  name: embeddingModelDeploymentName
  sku: {
    name: embeddingModelSku
    capacity: embeddingModelCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: embeddingModelName
      version: embeddingModelVersion
    }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
  dependsOn: [
    chatDeployment
  ]
}

// Memory extraction and search invoke models using the project's managed identity.
resource projectModelAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, project.id, foundryUserRoleId)
  scope: account
  properties: {
    principalId: project.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: foundryUserRoleId
  }
}

resource sampleMemoryAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(project.id, principalId, foundryUserRoleId)
  scope: project
  properties: {
    principalId: principalId
    principalType: principalType
    roleDefinitionId: foundryUserRoleId
  }
}

output FOUNDRY_PROJECT_ENDPOINT string = '${account.properties.endpoints['AI Foundry API']}/api/projects/${project.name}'
output CHAT_MODEL_DEPLOYMENT_NAME string = chatDeployment.name
output EMBEDDING_MODEL_DEPLOYMENT_NAME string = embeddingDeployment.name
