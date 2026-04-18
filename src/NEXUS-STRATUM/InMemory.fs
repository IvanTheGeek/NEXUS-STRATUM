namespace Stratum

open System
open System.Collections.Concurrent

/// InMemory IEventStore — ephemeral, thread-safe, no persistence.
/// Use for tests, browser-hosted (WASM) contexts, and initial development.
module InMemory =

    /// Creates a new, empty in-memory event store.
    let create () : IEventStore =

        // StreamId -> StoredEvent list (ordered, append-only)
        let streams = ConcurrentDictionary<string, StoredEvent list>()
        let gate    = obj ()

        { new IEventStore with

            member _.Append streamId expectedPosition events = async {
                let (StreamId id) = streamId
                let now = DateTimeOffset.UtcNow

                return
                    lock gate (fun () ->
                        let existing =
                            match streams.TryGetValue(id) with
                            | true, evts -> evts
                            | false, _   -> []

                        match existing with
                        | [] ->
                            match expectedPosition with
                            | At n ->
                                Error $"Optimistic concurrency conflict: expected position {n} but stream does not exist"
                            | Start | End ->
                                match events with
                                | [] -> Ok 0UL
                                | _  ->
                                    let stored =
                                        events
                                        |> List.mapi (fun i data ->
                                            { StreamId  = streamId
                                              Position  = uint64 i
                                              Data      = data
                                              CreatedAt = now })
                                    streams.[id] <- stored
                                    Ok (List.last stored).Position

                        | evts ->
                            let lastPos = (List.last evts).Position
                            match expectedPosition with
                            | At n when n <> lastPos ->
                                Error $"Optimistic concurrency conflict: expected position {n} but current is {lastPos}"
                            | _ ->
                                match events with
                                | [] -> Ok lastPos
                                | _  ->
                                    let nextPos = lastPos + 1UL
                                    let stored =
                                        events
                                        |> List.mapi (fun i data ->
                                            { StreamId  = streamId
                                              Position  = nextPos + uint64 i
                                              Data      = data
                                              CreatedAt = now })
                                    let updated = evts @ stored
                                    streams.[id] <- updated
                                    Ok (List.last updated).Position
                    )
            }

            member _.Read streamId from = async {
                let (StreamId id) = streamId
                match streams.TryGetValue(id) with
                | false, _ -> return []
                | true, evts ->
                    return
                        match from with
                        | Start -> evts
                        | End   -> []
                        | At n  -> evts |> List.filter (fun e -> e.Position >= n)
            }

            member _.Exists streamId = async {
                let (StreamId id) = streamId
                return streams.ContainsKey(id)
            }
        }
