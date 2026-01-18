# Fixora License Generator Tool

This tool is used internally to register customers and issue **signed Fixora licenses**.
It is **not distributed to customers**.

---

## What This Tool Does

- Registers companies (tenants)
- Issues signed Fixora licenses
- Stores:
  - License history (archive)
  - Latest license ready to send to the customer
- Signs licenses using **Azure Key Vault (RSA / RS256)**
- Produces licenses that Fixora verifies **offline**

---

## Prerequisites

### 1. .NET SDK
- .NET **8.0 or later**

Check:
```
dotnet --version
```

---

### 2. Azure Key Vault
You must have:
- An Azure subscription
- An Azure Key Vault
- An **RSA key** (2048+ bits recommended)

> The private key **never leaves Key Vault**.

---

### 3. Azure Authentication
The tool uses `DefaultAzureCredential`.

You must be logged in using **one** of:
- Azure CLI  
  ```bash
  az login
  ```
- Visual Studio / VS Code (signed in)
- Managed Identity (if running in Azure)

---

## Configuration

### appsettings.json (Required)

Create an `appsettings.json` file **next to the executable**:

```json
{
  "KeyVault": {
    "Name": "your-keyvault-name",
    "KeyName": "fixora-license-signing-key"
  }
}
```

---

## Data Storage Layout

All data is stored in a persistent directory:

```
<FixoraData>\LicenseIssuer\
└── tenants\
    └── {tenantId}\
        ├── company.json
        ├── licenses\
        │   └── {licenseId}\
        │       └── fixora.license.json   (archive / history)
        └── exports\
            └── fixora.license.json       (latest license to send)
```

---

## Commands

### Register a Company (Tenant)

```bash
dotnet run register-company
```

Prompts for:
- Company name
- Primary contact name
- Primary contact email
- Optional notes

Outputs:
- TenantId
- `company.json`
- CSV tracking entry

---

### Issue a License

```bash
dotnet run issue-license
```

Prompts for:
- TenantId
- License type (`pilot` / `paid`)
- Duration (days / months / years)
- Max endpoints
- Feature flags (`y/n`)

Outputs:
- Archived license (history)
- Latest export for customer
- CSV issuance record

---

## Feature Flags (MVP)

```json
"features": {
  "customWorkflows": false,
  "adminEnabled": false
}
```

- Always **false for MVP**
- Forward-compatible for future features

---

## License Model

- RSA + SHA256 (RS256)
- Canonical JSON payload
- Public key embedded in Fixora
- No online validation required

---

## What to Send to the Customer

Only send this file:

```
tenants/{tenantId}/exports/fixora.license.json
```

The customer places it into Fixora’s license folder.

---

## What NOT to Share

- Private keys
- Key Vault access
- Internal tenant folders
- CSV tracking files

---

## Status

✅ MVP ready  
✅ Enterprise-safe  
✅ Offline & tamper-proof  

---

This tool is intentionally simple, file-based, and easy to hand over.

