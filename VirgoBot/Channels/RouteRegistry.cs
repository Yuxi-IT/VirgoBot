using System.Net;
using System.Text.RegularExpressions;

namespace VirgoBot.Channels;

public class RouteRegistry
{
    private readonly List<RouteEntry> _routes = new();

    public void Register(string method, string pattern, Func<HttpListenerContext, Dictionary<string, string>, Task> handler)
    {
        var regex = BuildRegex(pattern, out var paramNames);
        _routes.Add(new RouteEntry(method.ToUpperInvariant(), pattern, regex, paramNames, handler));
    }

    public bool TryMatch(string method, string path, out Func<HttpListenerContext, Dictionary<string, string>, Task>? handler, out Dictionary<string, string> routeParams)
    {
        handler = null;
        routeParams = new Dictionary<string, string>();

        foreach (var route in _routes)
        {
            if (route.Method != method.ToUpperInvariant()) continue;

            var match = route.Regex.Match(path);
            if (!match.Success) continue;

            handler = route.Handler;
            for (int i = 0; i < route.ParamNames.Length; i++)
                routeParams[route.ParamNames[i]] = Uri.UnescapeDataString(match.Groups[i + 1].Value);
            return true;
        }

        return false;
    }

    private static Regex BuildRegex(string pattern, out string[] paramNames)
    {
        var names = new List<string>();
        // Escape dots, then replace {param} with capture groups
        var escaped = Regex.Escape(pattern).Replace("\\{", "{").Replace("\\}", "}");
        var regexPattern = Regex.Replace(escaped, @"\{(\w+)\}", m =>
        {
            names.Add(m.Groups[1].Value);
            return "([^/]+)";
        });
        paramNames = names.ToArray();
        return new Regex("^" + regexPattern + "$", RegexOptions.Compiled);
    }

    private record RouteEntry(
        string Method,
        string Pattern,
        Regex Regex,
        string[] ParamNames,
        Func<HttpListenerContext, Dictionary<string, string>, Task> Handler);
}
