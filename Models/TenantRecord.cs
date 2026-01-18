namespace Fixora.LicenseIssuer.Models;

public record TenantRecord(
    string TenantId,
    string CompanyName,
    string PrimaryContactName,
    string PrimaryContactEmail,
    string CreatedAtUtc,
    string Notes
);
