using Microsoft.Extensions.Caching.Memory;
using Continuo.Configuration.Abstractions;
using Continuo.Configuration.Models;

namespace SupportOpsApi.Services;

public sealed record SlaPolicy(IReadOnlyDictionary<string, int> Defaults, int WarnBeforeMinutes);

public sealed class SlaPolicyService {
    private const string Module = "support-ops";
    private const string Section = "work-item-sla";
    private const string WarnKey = "warn.beforeMinutes";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

    private static readonly Dictionary<string, int> DefaultSlaMinutes = new(StringComparer.OrdinalIgnoreCase) {
        ["task.p1"] = 240,
        ["task.p2"] = 480,
        ["task.p3"] = 1440,
        ["bug.p1"] = 120,
        ["bug.p2"] = 240,
        ["bug.p3"] = 720,
        ["analysis.p1"] = 480,
        ["analysis.p2"] = 1440,
        ["analysis.p3"] = 2880
    };

    private readonly IParameterProvider _provider;
    private readonly IMemoryCache _cache;

    public SlaPolicyService(IParameterProvider provider, IMemoryCache cache) {
        _provider = provider;
        _cache = cache;
    }

    public async Task<SlaPolicy> GetPolicyAsync(CancellationToken ct = default) {
        if (_cache.TryGetValue<SlaPolicy>(CacheKey(), out var cached)) {
            return cached!;
        }

        var scope = ParameterScope.Global();
        var values = await _provider.GetSectionAsync(Module, Section, scope, ct);
        var defaults = new Dictionary<string, int>(DefaultSlaMinutes, StringComparer.OrdinalIgnoreCase);
        var warnBefore = 60;

        foreach (var entry in values) {
            if (entry.Key.Equals(WarnKey, StringComparison.OrdinalIgnoreCase)) {
                warnBefore = entry.Value.AsInt(warnBefore);
                continue;
            }

            var minutes = entry.Value.AsInt();
            if (minutes > 0) {
                defaults[entry.Key] = minutes;
            }
        }

        var policy = new SlaPolicy(defaults, warnBefore);
        _cache.Set(CacheKey(), policy, CacheTtl);
        return policy;
    }

    public async Task<int> ResolveSlaMinutesAsync(string type, string priority, CancellationToken ct = default) {
        var policy = await GetPolicyAsync(ct);
        var key = $"{Normalize(type)}.{Normalize(priority)}";
        return policy.Defaults.TryGetValue(key, out var minutes) ? minutes : 0;
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string CacheKey() => $"{Module}:{Section}:policy";
}
