using System.Text.Json;

namespace VirgoBot.Functions;

public class FunctionDefinition
{
    public string Name { get; }
    public string Description { get; }
    public object InputSchema { get; }
    public Func<JsonElement, Task<string>> Handler { get; }
    public string Category { get; }

    public FunctionDefinition(string name, string description, object inputSchema, Func<JsonElement, Task<string>> handler, string category = "builtin")
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
        Handler = handler;
        Category = category;
    }
}
