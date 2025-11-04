# ADR 0002 — Windows Process Execution Abstraction

## Status
Accepted

## Context
Windows packaging workflows rely on multiple command-line tools (`makeappx`, `signtool`, `candle`, `light`, `winget`). Running them directly via `System.Diagnostics.Process` scatters orchestration logic across providers and complicates unit testing and remote agent execution. We need a single abstraction that can route commands locally or via remote executors.

## Decision
- Introduce `IProcessRunner` within `PackagingTools.Core.Windows.Tooling` to encapsulate process invocation details (working directory, environment variables, stdout/stderr capture).
- Format providers depend only on `IProcessRunner`, allowing the same code path to be reused for local builds and remote Windows agents.
- The interface is asynchronous and cancellation-aware, enabling clean termination when pipelines are aborted.

## Consequences
- Concrete implementations must be supplied by the host (GUI/CLI/service) — e.g., a local `ProcessRunner` and a remote-agent proxy.
- Unit tests can replace the runner with fakes to simulate tool output without executing real tooling.
- Additional telemetry hooks can be layered into the runner without modifying individual format providers.
