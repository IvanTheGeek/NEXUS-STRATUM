namespace Stratum

/// The set of storage backends STRATUM can target.
/// Each case carries only the configuration its backend needs.
type Backend =
    | InMemory
    | FileSystem  of rootPath: string
    | Dropbox     of accessToken: string
    | GoogleDrive of accessToken: string
    | DuckDB      of connectionString: string
    | VectorDB    of endpoint: string * collectionName: string

/// A stream is a named, ordered sequence of serialized events.
type StreamId = StreamId of string

/// Position within a stream, used for optimistic concurrency and reads.
type StreamPosition =
    | Start
    | At     of uint64
    | End

/// A raw stored event — payload is opaque bytes; the store does not interpret it.
type StoredEvent = {
    StreamId  : StreamId
    Position  : uint64
    Data      : byte[]
    CreatedAt : System.DateTimeOffset
}

/// Capabilities every STRATUM store must provide.
/// Implementations are one per Backend.
type IEventStore =
    abstract Append : StreamId -> expectedPosition: StreamPosition -> events: byte[] list -> Async<Result<uint64, string>>
    abstract Read   : StreamId -> from: StreamPosition -> Async<StoredEvent list>
    abstract Exists : StreamId -> Async<bool>
