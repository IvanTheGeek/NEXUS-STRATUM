namespace Stratum

open System

/// The set of storage backends STRATUM can target.
/// Each case carries only the configuration its backend needs.
type Backend =
    | InMemory
    | FileSystem  of rootPath: string
    | Dropbox     of accessToken: string
    | GoogleDrive of accessToken: string
    | DuckDB      of connectionString: string
    | VectorDB    of endpoint: string * collectionName: string

/// A stream is a named, ordered sequence of events.
type StreamId = StreamId of string

/// Position within a stream — used for reads.
type StreamPosition =
    | Start
    | At  of uint64
    | End

/// An event to be appended.
/// The caller supplies identity and causation metadata; the store assigns position and timestamp.
type EventToAppend = {
    EventId       : Guid    // UUIDv7 — generate with Guid.CreateVersion7()
    CorrelationId : Guid    // propagated unchanged from the originating command/request
    CausationId   : Guid    // ID of the command or event that directly caused this one
    Data          : byte[]
}

/// A stored event — EventToAppend fields plus store-assigned position and timestamp.
type StoredEvent = {
    EventId       : Guid
    CorrelationId : Guid
    CausationId   : Guid
    StreamId      : StreamId
    Position      : uint64          // stream-local ordinal, 0-based
    Data          : byte[]
    OccurredAt    : DateTimeOffset  // UTC, assigned by the store at write time
}

/// Capabilities every STRATUM backend must provide.
/// Append always succeeds — conflicts are domain events, not storage errors.
type IEventStore =
    abstract Append : StreamId -> events: EventToAppend list -> Async<uint64>
    abstract Read   : StreamId -> from: StreamPosition -> Async<StoredEvent list>
    abstract Exists : StreamId -> Async<bool>
