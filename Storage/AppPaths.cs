using System;

namespace Fixora.LicenseIssuer.Storage;

public static class AppPaths
{
    // Override with: setx FIXORA_ISSUER_DATA_DIR "D:\FixoraData\LicenseIssuer"
    public static string DataDir =>
        Environment.GetEnvironmentVariable("FIXORA_ISSUER_DATA_DIR")
        ?? @"C:\FixoraData\LicenseIssuer";
}
