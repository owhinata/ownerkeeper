# OwnerKeeper Release Notes

## v0.1.0 (Initial Preview)
- Scope: Phases 0–9 from docs/SCHEDULE.md
- Highlights:
  - API + Domain: Operation tickets (accepted/immediate-failure), state model, error codes
  - Resource management: single ownership, immediate conflict detection (OWN2001)
  - State machine: illegal transitions return immediate failure (ARG3001)
  - OperationScheduler: channel-based async execution, event dispatch, hardware adapter calls
  - Events: OperationCompleted with ResourceId, typed events bridged via OwnerSession
  - Host: initialization/shutdown, session creation, pre-register camera resources
  - Hardware abstraction: IHardwareResource, stub camera, default factory
  - Metrics: operation totals, failures (ARG/OWN/HW), latency; debug logging hooks
- Tests: Unit + Integration (normal flow, conflicts, HW error event)

## Compatibility & Requirements
- .NET 8, C# 10; analyzers enabled and code style enforced

## Notes
- Illegal transitions and certain runtime failures return immediate-failure tickets
  instead of throwing exceptions (ARG3001, OWN2001). Not-initialized usage throws
  OwnerKeeperNotInitializedException (ARG3002).
- Metrics names (Meter):
  - ownerkeeper_operations_total{type}
  - ownerkeeper_operation_failures_total{type, error}
  - ownerkeeper_operation_latency_ms{type}

