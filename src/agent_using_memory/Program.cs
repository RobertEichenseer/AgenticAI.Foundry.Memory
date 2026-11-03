
#pragma warning disable OPENAI001

// Load environment variables from the workspace config file.
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.AI.Projects.Memory;
using Azure.Identity;
using DotNetEnv;
using OpenAI.Responses;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading;

var configPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..", "..", "..", "..", "..", "config", "config.env"));

Env.Load(configPath);

string foundryProjectEndpoint = Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT") 
    ?? throw new InvalidOperationException("Missing configuration value: FOUNDRY_PROJECT_ENDPOINT");
string chatModelDeploymentName = Environment.GetEnvironmentVariable("CHAT_MODEL_DEPLOYMENT_NAME") 
    ?? throw new InvalidOperationException("Missing configuration value: CHAT_MODEL_DEPLOYMENT_NAME");
string embeddingModelDeploymentName = Environment.GetEnvironmentVariable("EMBEDDING_MODEL_DEPLOYMENT_NAME") 
    ?? throw new InvalidOperationException("Missing configuration value: EMBEDDING_MODEL_DEPLOYMENT_NAME");

// user id for the memory scope
string memoryScope = Environment.GetEnvironmentVariable("MEMORY_SCOPE") 
    ?? throw new InvalidOperationException("Missing configuration value: MEMORY_SCOPE");
string memoryStoreName = Environment.GetEnvironmentVariable("MEMORY_STORE_NAME") 
    ?? throw new InvalidOperationException("Missing configuration value: MEMORY_STORE_NAME");


// Configure Azure credentials
DefaultAzureCredentialOptions defaultAzureCredentialOptions = new DefaultAzureCredentialOptions()
{
    ExcludeAzureCliCredential = false,
    ExcludeVisualStudioCredential = false,
    ExcludeVisualStudioCodeCredential = false,
    ExcludeManagedIdentityCredential = true,
    ExcludeInteractiveBrowserCredential = true,
    ExcludeEnvironmentCredential = true
};
DefaultAzureCredential defaultAzureCredential = new DefaultAzureCredential(defaultAzureCredentialOptions);

// Create the AI Project client
AIProjectClient aiProjectClient = new AIProjectClient(
    new Uri(foundryProjectEndpoint),
    defaultAzureCredential
);

// Create a prompt agent - add the memory search tool to it
string agentName = "projectOptimizer";

MemorySearchPreviewTool memorySearchTool = new MemorySearchPreviewTool(memoryStoreName, memoryScope);
memorySearchTool.UpdateDelayInSecs = 1;

string agentInstructions = @"
    You are a helpful assistant that helps optimizing processes. 
    If necessary you use tools to identify communication preferences.
";

DeclarativeAgentDefinition declarativeAgentDefintion = new DeclarativeAgentDefinition(chatModelDeploymentName) {
    Instructions = agentInstructions,
    Tools = { memorySearchTool }
};

ProjectsAgentVersionCreationOptions projectsAgentVersionCreationOptions = new ProjectsAgentVersionCreationOptions(declarativeAgentDefintion);
ProjectsAgentVersion projectsAgentVersion =
    aiProjectClient.AgentAdministrationClient.CreateAgentVersion(agentName, projectsAgentVersionCreationOptions);

// Communicate with the Agent
ProjectResponsesClient responseClient = aiProjectClient
    .ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(projectsAgentVersion.Name);

// Create an agent reference
AgentReference agentReference = new AgentReference(
    projectsAgentVersion.Name,
    projectsAgentVersion.Version
);

// Show first contact method as retrieved from memory store
string prompt = @"
    Send me the phrase 'I like it'. 
    Show me how you will contact me.
    Don't provide any further information.
";
ResponseItem responseItem = ResponseItem.CreateUserMessageItem(prompt);
ResponseResult responseResult = responseClient.CreateResponse([responseItem]);
Console.WriteLine($"Expected contact method: SMS (as provided via memory store)");
Console.WriteLine($"\t{responseResult.GetOutputText().Replace("\n", "").Replace("\r", "")}");

// Change preferred contact method to private e-mail & update memory store setting
prompt = @"
  Send me again 'I like it'.
  From now on preferred contact method is private e-mail.
";
responseItem = ResponseItem.CreateUserMessageItem(prompt);

MemoryUpdateOptions memoryUpdateOptions = new MemoryUpdateOptions(memoryScope) {
    UpdateDelay = 0
};
memoryUpdateOptions.Items.Add(responseItem);
aiProjectClient.MemoryStores.WaitForMemoriesUpdate(memoryStoreName, 500, memoryUpdateOptions);

responseResult = responseClient.CreateResponse([responseItem]);
Console.WriteLine($"Expected contact method: private e-mail");
Console.WriteLine($"\t{responseResult.GetOutputText().Replace("\n", "").Replace("\r", "")}");

prompt = @"
  Send me again 'I like it'.
  Provide the contact method.
";
responseItem = ResponseItem.CreateUserMessageItem(prompt);
responseResult = responseClient.CreateResponse([responseItem]);
Console.WriteLine($"Expected contact method: private e-mail");
Console.WriteLine($"\t{responseResult.GetOutputText().Replace("\n", "").Replace("\r", "")}");

