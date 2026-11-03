#pragma warning disable OPENAI001

using Azure.AI.Projects;
using Azure.AI.Projects.Memory;
using Azure.Identity;
using DotNetEnv;
using OpenAI.Responses;
using System.ClientModel;


// Load environment variables from the workspace config file.
// Resolve from bin\Debug\net10.0 (or bin\Release\net10.0) to the workspace config.
string configPath = Path.GetFullPath(Path.Combine(
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

// Create AI Project Client
AIProjectClient aiProjectClient = new AIProjectClient(
    new Uri(foundryProjectEndpoint),
    defaultAzureCredential
);

// Create Memory Store
MemoryStoreDefaultDefinition memoryStoreDefaultDefinition = new MemoryStoreDefaultDefinition(
    chatModel: chatModelDeploymentName,
    embeddingModel: embeddingModelDeploymentName
);

memoryStoreDefaultDefinition.Options = new MemoryStoreDefaultOptions(
    isUserProfileEnabled: true,
    isChatSummaryEnabled: true
);

memoryStoreDefaultDefinition.Options.UserProfileDetails = @"
    Process communication details like phone number, SMS, Whatsapp, Messenger IDs.
    Don't process sensitive data or other PII data
";

// Delete memory store in case it already exists
try
{
    aiProjectClient.MemoryStores.DeleteMemoryStore(memoryStoreName);
}
catch (ClientResultException exception) when (exception.Status == 404) {}

// Create memory store
MemoryStore memoryStore = aiProjectClient.MemoryStores.CreateMemoryStore(
    name: memoryStoreName,
    definition: memoryStoreDefaultDefinition,
    description: "Memory store demo"
);

// List existing memory stores
Console.WriteLine("Listing existing memory stores:");
foreach (MemoryStore existingMemoryStore in aiProjectClient.MemoryStores.GetMemoryStores())
{
    Console.WriteLine($"\t {existingMemoryStore.Name} - ({existingMemoryStore.Description})"
    );
}

// Write memories
MemoryUpdateOptions memoryUpdateOptions = new MemoryUpdateOptions(memoryScope)
{
    UpdateDelay = 0, // Trigger update immediately without waiting for inactivity
};

string userInformation = @"
    User preferes being contacted via SMS. 
    If SMS is not available Multifactor Authentication (Microsoft Authenticator) can be used.
";

memoryUpdateOptions.Items.Add(ResponseItem.CreateUserMessageItem(userInformation)); 
    
// Wait for the update operation to complete
MemoryUpdateResult updateResult =
    aiProjectClient.MemoryStores.WaitForMemoriesUpdate(
        memoryStoreName: memoryStore.Name,
        options: memoryUpdateOptions,
        pollingInterval: 50);

if (updateResult.Status == MemoryStoreUpdateStatus.Failed)
{
    throw new InvalidOperationException(
        updateResult.ErrorDetails);
}

foreach (MemoryOperation memoryOperation in updateResult.Details.MemoryOperations)
{
    Console.WriteLine($"Memory content: {memoryOperation.MemoryItem.Content}");
}

// Store additional information
memoryUpdateOptions = new MemoryUpdateOptions(memoryScope)
{
    PreviousUpdateId = updateResult.UpdateId,
    UpdateDelay = 0, // Trigger update immediately without waiting for inactivity
};

userInformation = @"
    I'm now also available on telegram.
";
memoryUpdateOptions.Items.Add(ResponseItem.CreateUserMessageItem(userInformation));

MemoryUpdateResult memoryUpdateResult =
    aiProjectClient.MemoryStores.WaitForMemoriesUpdate(
        memoryStoreName: memoryStore.Name,
        options: memoryUpdateOptions,
        pollingInterval: 50);

if (memoryUpdateResult.Status == MemoryStoreUpdateStatus.Failed)
{
    throw new InvalidOperationException(memoryUpdateResult.ErrorDetails);
}
foreach (MemoryOperation memoryOperation in memoryUpdateResult.Details.MemoryOperations)
{
    Console.WriteLine(
        $"Memory content: {memoryOperation.MemoryItem.Content}");
}

// Search memories by a query
MemorySearchResultOptions memorySearchResultOptions = new MemorySearchResultOptions() { MaxMemories = 5 }; 
MemorySearchOptions memorySearchOptions = new MemorySearchOptions(memoryScope)
{
    Items =
    {
        ResponseItem.CreateUserMessageItem("How do I want to be contacted?")
    },
    ResultOptions = memorySearchResultOptions,
};

MemoryStoreSearchResponse memoryStoreSearchResponse = aiProjectClient
    .MemoryStores
    .SearchMemories(
        memoryStoreName: memoryStore.Name,
        options: memorySearchOptions
    );

foreach (MemorySearchItem item in memoryStoreSearchResponse.Memories)
{
    Console.WriteLine($"\t memory content: {item.MemoryItem.Content}");
}

// Search Memory
#pragma warning disable OPENAI001
