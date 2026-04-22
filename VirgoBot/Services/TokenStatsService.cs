namespace VirgoBot.Services;

public class TokenStatsService
{
    private long _promptTokens;
    private long _completionTokens;
    private long _requestCount;

    public void Record(int promptTokens, int completionTokens)
    {
        Interlocked.Add(ref _promptTokens, promptTokens);
        Interlocked.Add(ref _completionTokens, completionTokens);
        Interlocked.Increment(ref _requestCount);
    }

    public (long PromptTokens, long CompletionTokens, long TotalTokens, long RequestCount) GetStats()
    {
        var prompt = Interlocked.Read(ref _promptTokens);
        var completion = Interlocked.Read(ref _completionTokens);
        return (prompt, completion, prompt + completion, Interlocked.Read(ref _requestCount));
    }
}
