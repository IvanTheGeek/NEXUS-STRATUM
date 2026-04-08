# NEXUS-STRATUM

> Workspace instructions and references: `../CLAUDE.md`, `../FSHARP.md`, `../WORKFLOW.md`

## Purpose

STRATUM is the storage abstraction layer for NEXUS projects and apps. It decouples application code from any specific storage concern by providing a single `IEventStore` interface with multiple backend implementations.

A `Backend` discriminated union expresses every supported target — the application picks one; STRATUM handles the rest.

### Backends (planned)
- `InMemory` — ephemeral; suitable for browser-side / in-process use
- `FileSystem` — persists event streams to local disk
- `Dropbox` — cloud persistence via Dropbox API
- `GoogleDrive` — cloud persistence via Google Drive API
- `DuckDB` — embedded analytical DB; good for local query-heavy workloads
- `VectorDB` — vector store endpoint; for embedding-aware event retrieval

### Core abstractions (`Core.fs`)
- `Backend` — DU of all supported storage targets
- `StreamId` — single-case DU wrapping a stream name
- `StreamPosition` — `Start | At n | End`; used for reads and optimistic concurrency
- `StoredEvent` — raw stored event (opaque `byte[]` payload; store does not interpret)
- `IEventStore` — `Append`, `Read`, `Exists`; every backend implements this

Each backend lives in its own file and provides a factory function that returns `IEventStore`.

## Stack

- Language: F# on .NET 10.0
- Solution: `NEXUS-STRATUM.slnx`
- Output: Library (`Stratum.dll`)
