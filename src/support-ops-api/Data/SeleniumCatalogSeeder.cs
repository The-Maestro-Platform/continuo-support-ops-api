using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Models;

namespace SupportOpsApi.Data;

/// <summary>
/// Seeds the Selenium test catalog from a build-generated <c>selenium-catalog.json</c>
/// manifest. The JSON is produced by the <c>SeleniumCatalogGenerator</c> tool which
/// reflects over the compiled <c>Continuo.SeleniumE2E</c> assembly and extracts
/// <c>[SeleniumTestMeta]</c> attributes (or auto-derives metadata from conventions).
///
/// New rows are inserted when the <see cref="SeleniumTest.Code"/> is missing. Rows
/// originally created by the seeder (<see cref="SeleniumTest.CreatedBy"/> = <c>"seeder"</c>)
/// also get their <see cref="SeleniumTest.CodeFullyQualifiedName"/> reconciled on startup
/// so that renamed/corrected method names flow through — user-edited rows are left alone.
/// </summary>
public static class SeleniumCatalogSeeder {
    private const string CatalogFileName = "selenium-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = true
    };

    public static async Task SeedAsync(TaskFlowDbContext db, CancellationToken ct = default) {
        var entries = LoadCatalogEntries();
        if (entries.Count == 0) {
            Console.WriteLine("[SeleniumCatalogSeeder] No catalog entries found — skipping.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var existing = await db.SeleniumTests.ToListAsync(ct);
        var byCode = existing.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var toAdd = new List<SeleniumTest>();
        var reconciled = 0;

        foreach (var entry in entries) {
            if (byCode.TryGetValue(entry.Code, out var row)) {
                // Only reconcile rows that the seeder originally created
                if (string.Equals(row.CreatedBy, "seeder", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(row.CodeFullyQualifiedName, entry.CodeFullyQualifiedName, StringComparison.Ordinal)) {
                    row.CodeFullyQualifiedName = entry.CodeFullyQualifiedName;
                    row.UpdatedAt = now;
                    reconciled++;
                }
                continue;
            }

            toAdd.Add(new SeleniumTest {
                Code = entry.Code,
                Name = entry.Name,
                Description = entry.Description,
                Kind = "code",
                CodeFullyQualifiedName = entry.CodeFullyQualifiedName,
                Tags = entry.Tags,
                TimeoutSeconds = entry.TimeoutSeconds > 0 ? entry.TimeoutSeconds : 900,
                IsActive = true,
                CreatedBy = "seeder",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (toAdd.Count > 0) {
            db.SeleniumTests.AddRange(toAdd);
        }

        if (toAdd.Count > 0 || reconciled > 0) {
            await db.SaveChangesAsync(ct);
            Console.WriteLine($"[SeleniumCatalogSeeder] Added {toAdd.Count}, reconciled {reconciled} (total catalog: {entries.Count})");
        } else {
            Console.WriteLine($"[SeleniumCatalogSeeder] Catalog up-to-date ({entries.Count} entries).");
        }
    }

    private static List<CatalogEntry> LoadCatalogEntries() {
        // 1. Try loading from file next to the running assembly (copied via CopyToOutputDirectory)
        var asmDir = Path.GetDirectoryName(typeof(SeleniumCatalogSeeder).Assembly.Location);
        if (asmDir != null) {
            // Check in Data/ subdirectory (Link path) and root
            foreach (var candidate in new[] {
                Path.Combine(asmDir, "Data", CatalogFileName),
                Path.Combine(asmDir, CatalogFileName)
            }) {
                if (File.Exists(candidate)) {
                    Console.WriteLine($"[SeleniumCatalogSeeder] Loading catalog from {candidate}");
                    var json = File.ReadAllText(candidate);
                    return JsonSerializer.Deserialize<List<CatalogEntry>>(json, JsonOptions) ?? [];
                }
            }
        }

        // 2. Try well-known relative path (dev scenario: running from repo root)
        var devPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tests", "Continuo.SeleniumE2E", CatalogFileName);
        if (File.Exists(devPath)) {
            Console.WriteLine($"[SeleniumCatalogSeeder] Loading catalog from dev path: {Path.GetFullPath(devPath)}");
            var json = File.ReadAllText(devPath);
            return JsonSerializer.Deserialize<List<CatalogEntry>>(json, JsonOptions) ?? [];
        }

        Console.WriteLine($"[SeleniumCatalogSeeder] WARNING: {CatalogFileName} not found. Build Continuo.SeleniumE2E to generate it.");
        return [];
    }

    private sealed record CatalogEntry(
        string Code,
        string Name,
        string CodeFullyQualifiedName,
        string Tags,
        string Description,
        int TimeoutSeconds);
}
