namespace Stratum

open System
open System.Collections.Concurrent

/// InMemory IEventStore — ephemeral, thread-safe, no persistence.
/// Use for tests, browser-hosted (WASM) contexts, and initial development.
module InMemory =

    type private StreamMsg =
        | Append of events: EventToAppend list * reply: AsyncReplyChannel<uint64>
        | Read   of from: StreamPosition       * reply: AsyncReplyChannel<StoredEvent list>

    let private createStreamAgent (streamId: StreamId) =
        MailboxProcessor<StreamMsg>.Start(fun inbox ->
            let rec loop (stored: StoredEvent list) = async {
                let! msg = inbox.Receive()
                match msg with

                | Append ([], reply) ->
                    // no-op: return current last position (0UL if stream is new)
                    let pos = match stored with [] -> 0UL | _ -> (List.last stored).Position
                    reply.Reply pos
                    return! loop stored

                | Append (events, reply) ->
                    let nextPos =
                        match stored with
                        | [] -> 0UL
                        | _  -> (List.last stored).Position + 1UL
                    let now = DateTimeOffset.UtcNow
                    let appended =
                        events
                        |> List.mapi (fun i e ->
                            { EventId       = e.EventId
                              CorrelationId = e.CorrelationId
                              CausationId   = e.CausationId
                              StreamId      = streamId
                              Position      = nextPos + uint64 i
                              Data          = e.Data
                              OccurredAt    = now })
                    let updated = stored @ appended
                    // Reply before continuing the loop — "accepted" semantics, not "processed"
                    reply.Reply (List.last updated).Position
                    return! loop updated

                | Read (from, reply) ->
                    let result =
                        match from with
                        | Start -> stored
                        | End   -> []
                        | At n  -> stored |> List.filter (fun e -> e.Position >= n)
                    reply.Reply result
                    return! loop stored
            }
            loop []
        )

    /// Creates a new, empty in-memory event store.
    /// Each stream gets its own MailboxProcessor — writes are serialized per stream, not globally.
    let create () : IEventStore =

        // Stream agent registry — GetOrAdd is atomic; agents are never removed
        let agents = ConcurrentDictionary<string, MailboxProcessor<StreamMsg>>()

        let getAgent streamId =
            let (StreamId id) = streamId
            agents.GetOrAdd(id, fun _ -> createStreamAgent streamId)

        { new IEventStore with

            member _.Append streamId events =
                (getAgent streamId).PostAndAsyncReply(fun ch -> Append (events, ch))

            member _.Read streamId from =
                let (StreamId id) = streamId
                match agents.TryGetValue(id) with
                | false, _    -> async { return [] }
                | true, agent -> agent.PostAndAsyncReply(fun ch -> Read (from, ch))

            member _.Exists streamId =
                let (StreamId id) = streamId
                async { return agents.ContainsKey(id) }
        }
