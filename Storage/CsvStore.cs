using System.Text;
using Fixora.LicenseIssuer.Models;

namespace Fixora.LicenseIssuer.Storage;

public static class CsvStore
{
    public static string TenantsFileName => "fixora-tenants.csv";
    public static string LicensesFileName => "fixora-licenses.csv";

    public static string TenantsFilePath => Path.Combine(AppPaths.DataDir, TenantsFileName);
    public static string LicensesFilePath => Path.Combine(AppPaths.DataDir, LicensesFileName);

    public static void EnsureBaseDir()
    {
        Directory.CreateDirectory(AppPaths.DataDir);
    }

    public static void EnsureTenantsCsvExists()
    {
        EnsureBaseDir();

        if (File.Exists(TenantsFilePath)) return;
        var header = "tenantId,companyName,primaryContactName,primaryContactEmail,createdAtUtc,notes";
        File.WriteAllText(TenantsFilePath, header + Environment.NewLine, Encoding.UTF8);
    }

    public static void EnsureLicensesCsvExists()
    {
        EnsureBaseDir();

        if (File.Exists(LicensesFilePath)) return;
        var header = "licenseId,tenantId,licenseType,issuedAtUtc,expiryUtc,maxEndpoints,featuresJson,outputFile,keyId";
        File.WriteAllText(LicensesFilePath, header + Environment.NewLine, Encoding.UTF8);
    }

    public static void AppendTenant(TenantRecord tenant)
    {
        EnsureTenantsCsvExists();

        var line = string.Join(",",
            Escape(tenant.TenantId),
            Escape(tenant.CompanyName),
            Escape(tenant.PrimaryContactName),
            Escape(tenant.PrimaryContactEmail),
            Escape(tenant.CreatedAtUtc),
            Escape(tenant.Notes)
        );

        File.AppendAllText(TenantsFilePath, line + Environment.NewLine, Encoding.UTF8);
    }

    public static void AppendLicense(LicenseRecord license)
    {
        EnsureLicensesCsvExists();

        var line = string.Join(",",
            Escape(license.LicenseId),
            Escape(license.TenantId),
            Escape(license.LicenseType),
            Escape(license.IssuedAtUtc),
            Escape(license.ExpiryUtc),
            Escape(license.MaxEndpoints),
            Escape(license.FeaturesJson),
            Escape(license.OutputFile),
            Escape(license.KeyId)
        );

        File.AppendAllText(LicensesFilePath, line + Environment.NewLine, Encoding.UTF8);
    }

    private static string Escape(string value)
    {
        value ??= "";
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        value = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{value}\"" : value;
    }
}
