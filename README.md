# OwnerKeeper

OwnerKeeper is a .NET 8 library for safe ownership and lifecycle
management of hardware-like resources (e.g., cameras). It exposes
synchronous APIs that return operation tickets while executing work
asynchronously, with strict single-ownership guarantees and clear
failure semantics.

- Single ownership per resource; conflicts fail immediately (OWN2001)
- State machine driven operations with illegal transitions as immediate
  failures (ARG3001)
- OperationScheduler (channel-based) + event dispatch via EventHub
- Typed session events bridged by `OwnerSession`
- Hardware abstraction (`IHardwareResource`) with a default camera stub
- Optional metrics (totals, failures, latency) and debug logging

See also: `RELEASE_NOTES.md`, `docs/` and `AGENTS.md`.

## Requirements
- .NET 8 SDK

## Quick Start
```csharp
using System;
using OwnerKeeper;
using OwnerKeeper.API;
using OwnerKeeper.Domain;

// Initialize once (idempotent)
OwnerKeeperHost.Instance.Initialize(new OwnerKeeperOptions
{
    CameraCount = 1,
    AutoRegisterMetrics = true,
    DebugMode = false,
});

// Create a session and subscribe to a typed event
var session = OwnerKeeperHost.Instance.CreateSession("user-1");
session.StartStreamingCompleted += (s, e) =>
{
    Console.WriteLine($"Start completed: success={e.IsSuccess} state={e.State} error={e.ErrorCode}");
};

// Start streaming (Ready → Streaming)
var ticket = session.StartStreaming();
Console.WriteLine($"Ticket: {ticket.OperationId}, status={ticket.Status}");

// ... later
session.StopStreaming();
OwnerKeeperHost.Instance.Shutdown();
```

## Failure Semantics (Immediate Failure Policy)
Synchronous APIs return an `OperationTicket` indicating either:
- `Accepted`: operation enqueued; completion event will be raised
- `FailedImmediately`: no async execution; failure reason is in `ErrorCode`

Typical immediate failures:
- Ownership conflict: `OWN2001`
- Illegal transition: `ARG3001`
- Pre-canceled token: `CT0001`
- Not initialized (misuse): throws `OwnerKeeperNotInitializedException (ARG3002)`

## Metrics & Logging
- Metrics (optional):
  - Counters: `ownerkeeper_operations_total{type}`, `ownerkeeper_operation_failures_total{type,error}`
  - Histogram: `ownerkeeper_operation_latency_ms{type}`
- Logging: simple console logger with `Info`/`Warning`/`Error`

Enable via `OwnerKeeperOptions` (e.g., `AutoRegisterMetrics = true`).

## Hardware Abstraction
- Implement `OwnerKeeper.Hardware.IHardwareResource` to integrate real devices.
- A `CameraStub` is provided for testing. Override the factory with
  `OwnerKeeperOptions.HardwareFactory` during initialization if needed.

## Build & Test
```bash
# From repository root
cd csharp
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet build OwnerKeeper.sln -v minimal
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test OwnerKeeper.sln -v minimal
```

## Code Coverage
Coverage is collected via Coverlet (MSBuild integration already referenced in `OwnerKeeper.Tests`):

```bash
# Cobertura XML output
dotnet test \
  /p:CollectCoverage=true \
  /p:CoverletOutput=TestResults/coverage/ \
  /p:CoverletOutputFormat=cobertura

# LCOV output (e.g., for SonarQube or front-end tooling)
dotnet test \
  /p:CollectCoverage=true \
  /p:CoverletOutput=TestResults/coverage/ \
  /p:CoverletOutputFormat=lcov
```

Additional formats are available by changing `CoverletOutputFormat` (e.g., `json`, `opencover`).

## Developer Notes
- Code style: analyzers enabled, warnings treated as errors
- Comments & XML docs: English (see `AGENTS.md`)
- Pre-commit formatting: CSharpier only
  - Install hook: `bash scripts/install-git-hooks.sh`

## Release Notes
See `RELEASE_NOTES.md`.

## License
See `LICENSE`.
