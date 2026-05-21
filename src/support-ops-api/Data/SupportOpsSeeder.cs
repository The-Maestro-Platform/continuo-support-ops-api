using Continuo.Persistence;

namespace SupportOpsApi.Data;

public class SupportOpsSeeder : IDatabaseSeeder {
    public async Task SeedAsync(CancellationToken cancellationToken = default) {
        // Intentionally left blank: default support seed removed.
        await Task.CompletedTask;
    }
}
