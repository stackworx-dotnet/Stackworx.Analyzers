---
sidebar_position: 8
---

# SW103: Avoid Microsoft.Extensions.Azure usage

## Overview

**Rule ID:** SW103  
**Category:** Architecture  
**Severity:** Warning  
**Status:** Enabled by default

## Description

This rule flags source usage of `Microsoft.Extensions.Azure` APIs (including `using Microsoft.Extensions.Azure;` and calls to symbols in that namespace).

The preferred direction is keyed services registration/resolution instead of `Microsoft.Extensions.Azure` builder-based registration.

## When This Rule Triggers

The analyzer reports a diagnostic when:
- A `using` directive imports `Microsoft.Extensions.Azure` (or a child namespace)
- A method call or object creation targets a symbol in `Microsoft.Extensions.Azure` (or a child namespace)

## Why This Matters

`Microsoft.Extensions.Azure` registration patterns (`AddAzureClients`, factory builders, etc.) make named registrations harder to reason about across large solutions.

Keyed services provide a clearer DI model where each Azure dependency is resolved with an explicit key and can be mapped directly to an Aspire resource binding.

## Migration Notes (for automated and manual migrations)

Use this checklist when migrating code flagged by SW103:

1. Identify `AddAzureClients(...)` blocks and collect each registered client name/key.
2. Replace the `Microsoft.Extensions.Azure` registration with keyed DI registrations for the concrete Azure SDK client types.
3. Keep one stable key per resource (for example, `"blob-storage"`), and use that same key at injection/resolution sites.
4. Update constructors and call sites to resolve keyed services instead of `IAzureClientFactory<TClient>`.
5. Remove `using Microsoft.Extensions.Azure;` once all references are migrated.
6. Validate startup and integration tests to ensure each keyed binding resolves correctly.

### Code samples

#### Registration — before (`AddAzureClients`)

```csharp
// Program.cs / Startup.cs
using Microsoft.Extensions.Azure;

builder.Services.AddAzureClients(clients =>
{
    clients.AddBlobServiceClient(builder.Configuration.GetSection("AzureStorage:Blob"))
           .WithName("blob-storage");

    clients.AddQueueServiceClient(builder.Configuration.GetSection("AzureStorage:Queue"))
           .WithName("order-queue");
});
```

#### Registration — after (keyed services)

```csharp
// Program.cs / Startup.cs
// No Microsoft.Extensions.Azure import required

builder.Services.AddKeyedSingleton<BlobServiceClient>("blob-storage", (sp, _) =>
    new BlobServiceClient(
        builder.Configuration.GetConnectionString("BlobStorage")));

builder.Services.AddKeyedSingleton<QueueServiceClient>("order-queue", (sp, _) =>
    new QueueServiceClient(
        builder.Configuration.GetConnectionString("OrderQueue")));
```

#### DI callsite — before (`IAzureClientFactory<T>`)

```csharp
public class BlobUploadService
{
    private readonly BlobServiceClient _blobClient;

    public BlobUploadService(IAzureClientFactory<BlobServiceClient> factory)
    {
        _blobClient = factory.CreateClient("blob-storage");
    }
}
```

#### DI callsite — after (keyed injection)

```csharp
public class BlobUploadService
{
    private readonly BlobServiceClient _blobClient;

    public BlobUploadService(
        [FromKeyedServices("blob-storage")] BlobServiceClient blobClient)
    {
        _blobClient = blobClient;
    }
}
```

### Suggested review points after migration

- Every previous `name:`/`WithName(...)` registration has a corresponding keyed registration.
- Prefer keyed singleton registrations for Azure SDK clients; avoid adding a default unkeyed registration for the same client type.
- No remaining references to `Microsoft.Extensions.Azure` types (`IAzureClientFactory<>`, builder types, extension methods).
- Key names are centralized and reused consistently.
- Resource keys align with Aspire integration naming.

## Related Documentation

- Aspire Azure Storage Blobs integration (keyed/resource-oriented approach):  
  https://aspire.dev/integrations/cloud/azure/azure-storage-blobs/azure-storage-blobs-connect/#blob-storage-resource
- Azure SDK discussion:  
  https://github.com/Azure/azure-sdk-for-net/issues/40408#issuecomment-3599496883
- Azure SDK follow-up issue:  
  https://github.com/Azure/azure-sdk-for-net/issues/55491

## Configuration

To disable:

```ini
[*.cs]
dotnet_diagnostic.SW103.severity = none
```

To enforce as error:

```ini
[*.cs]
dotnet_diagnostic.SW103.severity = error
```
