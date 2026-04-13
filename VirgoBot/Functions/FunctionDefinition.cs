using System.Text.Json;

namespace VirgoBot.Functions;

public class FunctionDefinition
{
    public string Name { get; }
    public string Description { get; }
    public object InputSchema { get; }
    public Func<JsonElement, Task<string>> Handler { get; }

    public FunctionDefinition(string name, string description, object inputSchema, Func<JsonElement, Task<string>> handler)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        Handler = handler;
    }
}
