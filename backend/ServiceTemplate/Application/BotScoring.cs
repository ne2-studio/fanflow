using ServiceTemplate.Ports.Output;

namespace ServiceTemplate.Application;

/// <summary>
/// Pure, deterministic bot-scoring heuristic (0-100) per CONTRACT.md's AnalyzePageView /
/// AnalyzeDestinationClick rules: known-crawler user agents, request frequency, dwell time
/// (DestinationClick only), and Cloudflare signals if available (none are wired for MVP).
/// Weights/thresholds are an engineering default, not a business-confirmed figure —
/// TODO: confirm scoring weights with business/data before relying on this for spend decisions.
/// </summary>
public static class BotScoring
{
    public static readonly TimeSpan RequestFrequencyWindow = TimeSpan.FromMinutes(1);

    private const int CrawlerUserAgentScore = 70;
    private const int RequestFrequencyScore = 30;
    private const int RequestFrequencyThreshold = 20;
    private const int FastDwellScore = 40;
    private const int FastDwellThresholdMs = 300;
    private const int BotClassificationThreshold = 50;

    private static readonly string[] KnownCrawlerSignatures =
    [
        "bot", "spider", "crawler", "googlebot", "bingbot", "slurp", "duckduckbot",
        "baiduspider", "yandexbot", "facebookexternalhit", "twitterbot", "applebot",
        "ahrefsbot", "semrushbot", "mj12bot", "dotbot", "petalbot", "bytespider", "curl", "wget"
    ];

    public static (int Score, EventClassification Classification) Score(TrackedEvent trackedEvent, int recentRequestsFromIp)
    {
        var score = 0;

        if (IsKnownCrawler(trackedEvent.UserAgent))
            score += CrawlerUserAgentScore;

        if (recentRequestsFromIp > RequestFrequencyThreshold)
            score += RequestFrequencyScore;

        if (trackedEvent.Type == EventType.DestinationClick && trackedEvent.DwellTimeMs is < FastDwellThresholdMs)
            score += FastDwellScore;

        score = Math.Min(score, 100);

        var classification = score >= BotClassificationThreshold ? EventClassification.Bot : EventClassification.Human;
        return (score, classification);
    }

    private static bool IsKnownCrawler(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return true;

        return KnownCrawlerSignatures.Any(signature =>
            userAgent.Contains(signature, StringComparison.OrdinalIgnoreCase));
    }
}
