using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Extensions.Configuration;
using Fixora.LicenseIssuer.Models;
using Fixora.LicenseIssuer.Storage;

// =====================
// Helpers
// =====================

static string ToIsoUtc(DateTime dtUtc) =>
    dtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

static string Prompt(string label, bool required = true)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var input = Console.ReadLine()?.Trim() ?? "";

        if (!required) return input;
        if (!string.IsNullOrWhiteSpace(input)) return input;

        Console.WriteLine("Required. Please enter a value.");
    }
}

static int PromptInt(string label, int? min = null, int? max = null)
{
    while (true)
    {
        var s = Prompt(label);
        if (int.TryParse(s, out var v))
        {
            if (min.HasValue && v < min.Value) { Console.WriteLine($"Must be >= {min.Value}"); continue; }
            if (max.HasValue && v > max.Value) { Console.WriteLine($"Must be <= {max.Value}"); continue; }
            return v;
        }
        Console.WriteLine("Please enter a whole number.");
    }
}

static bool PromptBool(string label)
{
    while (true)
    {
        var s = Prompt(label).Trim().ToLowerInvariant();
        if (s is "true" or "t" or "yes" or "y" or "1") return true;
        if (s is "false" or "f" or "no" or "n" or "0") return false;
        Console.WriteLine("Enter true/false (or y/n).");
    }
}

static string PromptDurationUnit()
{
    while (true)
    {
        Console.Write("Duration unit (1=days, 2=months, 3=years): ");
        var s = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

        if (s is "1" or "days" or "day") return "days";
        if (s is "2" or "months" or "month") return "months";
        if (s is "3" or "years" or "year") return "years";

        Console.WriteLine("Enter 1, 2, or 3 (or type days/months/years).");
    }
}

static DateTime CalculateExpiryUtc(DateTime issuedAtUtc, string unit, int value)
{
    var expiry = unit switch
    {
        "days" => issuedAtUtc.AddDays(value),
        "months" => issuedAtUtc.AddMonths(value),
        "years" => issuedAtUtc.AddYears(value),
        _ => throw new InvalidOperationException("Invalid duration unit.")
    };

    // End-of-day UTC
    return expiry.Date.AddDays(1).AddSeconds(-1);
}

static void ShowHelp()
{
    Console.WriteLine("Fixora License Tool");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  register-company");
    Console.WriteLine("  issue-license");
    Console.WriteLine();
}

// =====================
// JSON Canonicalisation
// =====================

static void WriteElement(Utf8JsonWriter writer, JsonElement el)
{
    switch (el.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (var prop in el.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(prop.Name);
                WriteElement(writer, prop.Value);
            }
            writer.WriteEndObject();
            break;

        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (var item in el.EnumerateArray())
                WriteElement(writer, item);
            writer.WriteEndArray();
            break;

        case JsonValueKind.String:
            writer.WriteStringValue(el.GetString());
            break;

        case JsonValueKind.Number:
            writer.WriteRawValue(el.GetRawText());
            break;

        case JsonValueKind.True:
        case JsonValueKind.False:
            writer.WriteBooleanValue(el.GetBoolean());
            break;

        case JsonValueKind.Null:
            writer.WriteNullValue();
            break;

        default:
            throw new NotSupportedException($"Unsupported JSON kind: {el.ValueKind}");
    }
}

static string CanonicalizeJson(JsonElement element)
{
    using var ms = new MemoryStream();
    using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
    WriteElement(writer, element);
    writer.Flush();
    return Encoding.UTF8.GetString(ms.ToArray());
}

// =====================
// START
// =====================

var command = (args.Length > 0 ? args[0] : "").Trim().ToLowerInvariant();

if (string.IsNullOrWhiteSpace(command))
{
    ShowHelp();
    return;
}

// Data dir (persisted)
var dataDir = AppPaths.DataDir;
Directory.CreateDirectory(dataDir);

// ---------------------
// REGISTER COMPANY
// ---------------------

if (command == "register-company")
{
    var companyName = Prompt("Company name");
    var contactName = Prompt("Primary contact name");
    var contactEmail = Prompt("Primary contact email");
    var notes = Prompt("Notes (optional)", required: false);

    var tenantId = Guid.NewGuid().ToString();
    var createdAtUtc = ToIsoUtc(DateTime.UtcNow);

    var tenantDir = Path.Combine(dataDir, "tenants", tenantId);
    Directory.CreateDirectory(tenantDir);

    var companyFilePath = Path.Combine(tenantDir, "company.json");

    var companyInternal = new
    {
        tenantId,
        companyName,
        primaryContactName = contactName,
        primaryContactEmail = contactEmail,
        createdAtUtc,
        notes
    };

    File.WriteAllText(
        companyFilePath,
        JsonSerializer.Serialize(companyInternal, new JsonSerializerOptions { WriteIndented = true }),
        Encoding.UTF8);

    CsvStore.AppendTenant(new TenantRecord(
        TenantId: tenantId,
        CompanyName: companyName,
        PrimaryContactName: contactName,
        PrimaryContactEmail: contactEmail,
        CreatedAtUtc: createdAtUtc,
        Notes: notes
    ));

    Console.WriteLine();
    Console.WriteLine("✅ Company registered");
    Console.WriteLine($"TenantId : {tenantId}");
    return;
}

// ---------------------
// ISSUE LICENSE
// ---------------------

if (command == "issue-license")
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .Build();

    var vaultName = configuration["KeyVault:Name"];
    var keyName = configuration["KeyVault:KeyName"];

    if (string.IsNullOrWhiteSpace(vaultName) || string.IsNullOrWhiteSpace(keyName))
        throw new InvalidOperationException("KeyVault config missing.");

    var tenantId = Prompt("TenantId");
    var licenseType = Prompt("License type (pilot/paid)").ToLowerInvariant();
    var durationUnit = PromptDurationUnit();
    var durationValue = PromptInt("Duration value", 1, 3650);
    var maxEndpoints = PromptInt("Max endpoints", 1, 100000);

    var customWorkflows = PromptBool("Feature: customWorkflows (y = yes / n = no)");
    var adminEnabled = PromptBool("Feature: adminEnabled (y = yes / n = no)");

    var licenseId = Guid.NewGuid().ToString();
    var issuedAtUtc = DateTime.UtcNow;
    var expiryUtc = CalculateExpiryUtc(issuedAtUtc, durationUnit, durationValue);

    var licensePayload = new
    {
        tenantId,
        licenseId,
        licenseType,
        issuedAtUtc = ToIsoUtc(issuedAtUtc),
        expiryUtc = ToIsoUtc(expiryUtc),
        maxEndpoints,
        features = new { customWorkflows, adminEnabled }
    };

    var payloadJson = JsonSerializer.Serialize(licensePayload);
    using var payloadDoc = JsonDocument.Parse(payloadJson);
    var canonicalPayloadJson = CanonicalizeJson(payloadDoc.RootElement);

    var credential = new DefaultAzureCredential();
    var vaultUri = new Uri($"https://{vaultName}.vault.azure.net/");
    var keyClient = new KeyClient(vaultUri, credential);
    var key = await keyClient.GetKeyAsync(keyName);

    var keyVersion = key.Value.Properties.Version!;
    var cryptoClient = new CryptographyClient(key.Value.Id, credential);
    var signature = await cryptoClient.SignDataAsync(
        SignatureAlgorithm.RS256,
        Encoding.UTF8.GetBytes(canonicalPayloadJson));

    var output = new
    {
        formatVersion = 1,
        keyId = keyVersion,
        license = JsonSerializer.Deserialize<JsonElement>(canonicalPayloadJson),
        signature = Convert.ToBase64String(signature.Signature)
    };

    var outputJson = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });

    // Archive
    var archiveDir = Path.Combine(dataDir, "tenants", tenantId, "licenses", licenseId);
    Directory.CreateDirectory(archiveDir);
    File.WriteAllText(Path.Combine(archiveDir, "fixora.license.json"), outputJson);

    // Export (latest)
    var exportDir = Path.Combine(dataDir, "tenants", tenantId, "exports");
    Directory.CreateDirectory(exportDir);
    var exportPath = Path.Combine(exportDir, "fixora.license.json");
    File.WriteAllText(exportPath, outputJson);

    CsvStore.AppendLicense(new LicenseRecord(
        LicenseId: licenseId,
        TenantId: tenantId,
        LicenseType: licenseType,
        IssuedAtUtc: ToIsoUtc(issuedAtUtc),
        ExpiryUtc: ToIsoUtc(expiryUtc),
        MaxEndpoints: maxEndpoints.ToString(),
        FeaturesJson: JsonSerializer.Serialize(new { customWorkflows, adminEnabled }),
        OutputFile: exportPath,
        KeyId: keyVersion
    ));

    Console.WriteLine();
    Console.WriteLine("✅ License issued");
    Console.WriteLine($"TenantId : {tenantId}");
    Console.WriteLine($"LicenseId: {licenseId}");
    Console.WriteLine($"Export   : {exportPath}");
    return;
}

ShowHelp();
