namespace Fixora.LicenseIssuer.Models;

public record LicenseRecord(
    string LicenseId,
    string TenantId,
    string LicenseType,
    string IssuedAtUtc,
    string ExpiryUtc,
    string MaxEndpoints,
    string FeaturesJson,
    string OutputFile,
    string KeyId
);
