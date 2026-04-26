namespace VirgoBot.Contracts;

public sealed record ChatImageBase64(string data, string? mediaType);

public sealed record ChatRequest(
    string? message,
    string? userId,
    string[]? imageUrls,
    ChatImageBase64[]? imageBase64);
