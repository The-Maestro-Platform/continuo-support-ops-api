namespace SupportOpsApi.Models;

/// <summary>
/// Bir Gitea push'unun unit/integration test çalışması — `unit-tests` workflow'u tarafından
/// kuyruğa alınır, sonuçlar `CiTestProjectResult` rows'larına ayrı ayrı bildirilir. Selenium
/// run'larından farkı: bu kuyruk runner-poll'lu değil, runner workflow'u zaten çalıştırıyor;
/// support-ops-api sadece pasif olarak result store + UI surface yapıyor.
///
/// Lifecycle: <c>queued</c> (workflow başladı) → <c>running</c> (testler koşuyor) →
/// <c>passed</c> / <c>failed</c> / <c>error</c> (build/runner crash).
/// </summary>
public class CiRun {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();

    /// <summary>Commit SHA (40 char, full). UI'da kısaltılır.</summary>
    public string CommitSha { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;

    /// <summary>Commit author (git config user.name veya gitea username).</summary>
    public string Author { get; set; } = string.Empty;
    public string? AuthorEmail { get; set; }

    /// <summary>Commit message (first line; full body uzunsa kırpılır).</summary>
    public string? CommitMessage { get; set; }

    /// <summary>Değişen dosyaların JSON array'i. ServiceDetector tarafından üretilir.</summary>
    public string? ChangedFilesJson { get; set; }

    /// <summary>
    /// Bu run'ın kapsadığı servis ismleri (CSV). <c>continuo-services.json</c> meta'sıyla
    /// resolve edilir — örn. `auth-api,order-api`. UI filtreleme ve dashboard için.
    /// </summary>
    public string ServicesInScope { get; set; } = string.Empty;

    public string Status { get; set; } = "queued"; // queued | running | passed | failed | error | cancelled

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public int DurationMs { get; set; }

    public int TotalProjects { get; set; }
    public int PassedProjects { get; set; }
    public int FailedProjects { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }

    /// <summary>Workflow run URL (Gitea Actions). UI'dan tıklanarak workflow log'una gidilebilir.</summary>
    public string? WorkflowRunUrl { get; set; }

    /// <summary>Gitea Actions runner host'unun ismi — birden fazla runner varsa hangisi koştu görmek için.</summary>
    public string? RunnerName { get; set; }

    /// <summary>Hangi env'de koşturuldu (`dev` / `staging`). Workflow değişkeninden gelir.</summary>
    public string EnvironmentScope { get; set; } = string.Empty;

    /// <summary>Build veya runner crash mesajı (`status = error` durumu için).</summary>
    public string? ErrorMessage { get; set; }

    public List<CiTestProjectResult> Projects { get; set; } = [];
}

/// <summary>
/// Tek bir test projesinin (örn. `tests/AuthApi.UnitTests`) sonucu. Workflow her proje için
/// `dotnet test --logger trx` çalıştırıp TRX'i parse eder ve buraya POST eder. Bu sayede UI
/// "OrderApi.UnitTests > Test_X_should_Y patladı" granülarisinde gösterebilir.
/// </summary>
public class CiTestProjectResult {
    public Guid Id { get; set; } = Ulid.NewUlid().ToGuid();
    public Guid RunId { get; set; }

    public string ProjectName { get; set; } = string.Empty; // örn. "AuthApi.UnitTests"
    public string ServiceName { get; set; } = string.Empty; // örn. "auth-api" (meta'dan resolve)

    public string Status { get; set; } = "running"; // running | passed | failed | error | skipped

    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public int DurationMs { get; set; }

    /// <summary>İlk N başarısız test'in adı + hata mesajı (JSON array). UI failure surface.</summary>
    public string? FailureDetailsJson { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }

    public CiRun? Run { get; set; }
}
