module Stratum.Tests.Tests

open System
open Expecto
open Stratum

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let private mkEvent () : EventToAppend =
    { EventId       = Guid.CreateVersion7()
      CorrelationId = Guid.NewGuid()
      CausationId   = Guid.NewGuid()
      EventType     = "TestEvent"
      Data          = [||] }

let private mkEventWithData (data: byte[]) : EventToAppend =
    { EventId       = Guid.CreateVersion7()
      CorrelationId = Guid.NewGuid()
      CausationId   = Guid.NewGuid()
      EventType     = "TestEvent"
      Data          = data }

let private run a = Async.RunSynchronously a

// ---------------------------------------------------------------------------
// Append
// ---------------------------------------------------------------------------

let appendTests =
    testList "Append" [

        testCase "first event in new stream lands at position 0" <| fun () ->
            let store = InMemory.create()
            let pos = store.Append (StreamId "s") [mkEvent()] |> run
            Expect.equal pos 0UL ""

        testCase "batch of N events returns last position N-1" <| fun () ->
            let store = InMemory.create()
            let pos = store.Append (StreamId "s") (List.init 5 (fun _ -> mkEvent())) |> run
            Expect.equal pos 4UL ""

        testCase "sequential single-event appends increment position by 1" <| fun () ->
            let store = InMemory.create()
            let sid   = StreamId "s"
            let pos0  = store.Append sid [mkEvent()] |> run
            let pos1  = store.Append sid [mkEvent()] |> run
            let pos2  = store.Append sid [mkEvent()] |> run
            Expect.equal pos0 0UL "first"
            Expect.equal pos1 1UL "second"
            Expect.equal pos2 2UL "third"

        testCase "appending empty list is a no-op and returns current position" <| fun () ->
            let store = InMemory.create()
            let sid   = StreamId "s"
            let _     = store.Append sid [mkEvent()] |> run   // pos 0
            let pos   = store.Append sid []           |> run
            Expect.equal pos 0UL ""

        testCase "streams are independent — appends to one do not affect another" <| fun () ->
            let store = InMemory.create()
            let _     = store.Append (StreamId "a") (List.init 3 (fun _ -> mkEvent())) |> run
            let pos   = store.Append (StreamId "b") [mkEvent()] |> run
            Expect.equal pos 0UL "stream b is unaffected by stream a"
    ]

// ---------------------------------------------------------------------------
// Read
// ---------------------------------------------------------------------------

let readTests =
    testList "Read" [

        testCase "Read Start on nonexistent stream returns empty" <| fun () ->
            let store  = InMemory.create()
            let events = store.Read (StreamId "missing") Start |> run
            Expect.isEmpty events ""

        testCase "Read Start returns all appended events in order" <| fun () ->
            let store  = InMemory.create()
            let sid    = StreamId "s"
            let _      = store.Append sid (List.init 4 (fun _ -> mkEvent())) |> run
            let events = store.Read sid Start |> run
            Expect.equal (List.length events) 4 "count"
            let positions = events |> List.map (fun e -> e.Position)
            Expect.equal positions [0UL; 1UL; 2UL; 3UL] "positions in order"

        testCase "Read End returns empty" <| fun () ->
            let store  = InMemory.create()
            let sid    = StreamId "s"
            let _      = store.Append sid [mkEvent()] |> run
            let events = store.Read sid End |> run
            Expect.isEmpty events ""

        testCase "Read At n returns only events from position n onward" <| fun () ->
            let store  = InMemory.create()
            let sid    = StreamId "s"
            let _      = store.Append sid (List.init 5 (fun _ -> mkEvent())) |> run
            let events = store.Read sid (At 3UL) |> run
            let positions = events |> List.map (fun e -> e.Position)
            Expect.equal positions [3UL; 4UL] ""

        testCase "Read At 0 is equivalent to Read Start" <| fun () ->
            let store  = InMemory.create()
            let sid    = StreamId "s"
            let _      = store.Append sid (List.init 3 (fun _ -> mkEvent())) |> run
            let fromStart = store.Read sid Start  |> run
            let fromZero  = store.Read sid (At 0UL) |> run
            Expect.equal fromZero fromStart ""
    ]

// ---------------------------------------------------------------------------
// Exists
// ---------------------------------------------------------------------------

let existsTests =
    testList "Exists" [

        testCase "Exists returns false for unknown stream" <| fun () ->
            let store = InMemory.create()
            let found = store.Exists (StreamId "unknown") |> run
            Expect.isFalse found ""

        testCase "Exists returns true after first append" <| fun () ->
            let store = InMemory.create()
            let sid   = StreamId "s"
            let _     = store.Append sid [mkEvent()] |> run
            let found = store.Exists sid |> run
            Expect.isTrue found ""
    ]

// ---------------------------------------------------------------------------
// Metadata round-trip
// ---------------------------------------------------------------------------

let metadataTests =
    testList "Metadata" [

        testCase "EventId, CorrelationId, CausationId, EventType survive the store round-trip" <| fun () ->
            let store     = InMemory.create()
            let evtId     = Guid.CreateVersion7()
            let corrId    = Guid.NewGuid()
            let cauId     = Guid.NewGuid()
            let eventType = "SomethingHappened"
            let evt       = { EventId = evtId; CorrelationId = corrId; CausationId = cauId; EventType = eventType; Data = [||] }
            let _         = store.Append (StreamId "s") [evt] |> run
            let stored    = store.Read (StreamId "s") Start   |> run
            Expect.equal stored.[0].EventId       evtId     "EventId"
            Expect.equal stored.[0].CorrelationId corrId    "CorrelationId"
            Expect.equal stored.[0].CausationId   cauId     "CausationId"
            Expect.equal stored.[0].EventType     eventType "EventType"

        testCase "event data survives the store round-trip" <| fun () ->
            let store = InMemory.create()
            let data  = [| 1uy; 2uy; 3uy; 255uy |]
            let _     = store.Append (StreamId "s") [mkEventWithData data] |> run
            let stored = store.Read (StreamId "s") Start |> run
            Expect.equal stored.[0].Data data ""

        testCase "OccurredAt is set to a recent UTC time" <| fun () ->
            let before = DateTimeOffset.UtcNow
            let store  = InMemory.create()
            let _      = store.Append (StreamId "s") [mkEvent()] |> run
            let after  = DateTimeOffset.UtcNow
            let stored = store.Read (StreamId "s") Start |> run
            Expect.isGreaterThanOrEqual stored.[0].OccurredAt before "not before write"
            Expect.isLessThanOrEqual    stored.[0].OccurredAt after  "not after write"

        testCase "StreamId is preserved on stored events" <| fun () ->
            let store = InMemory.create()
            let sid   = StreamId "laundry-expense-abc123"
            let _     = store.Append sid [mkEvent()] |> run
            let stored = store.Read sid Start |> run
            Expect.equal stored.[0].StreamId sid ""
    ]

// ---------------------------------------------------------------------------
// Concurrency
// ---------------------------------------------------------------------------

let concurrencyTests =
    testList "Concurrency" [

        testCase "concurrent appends to same stream produce no duplicate positions" <| fun () ->
            let store   = InMemory.create()
            let sid     = StreamId "concurrent"
            let appends = List.init 20 (fun _ -> store.Append sid [mkEvent()])
            let positions =
                Async.Parallel appends
                |> run
                |> Array.toList
                |> List.sort
            let expected = List.init 20 uint64
            Expect.equal positions expected "positions are 0-19 with no duplicates"

        testCase "concurrent appends to different streams do not interfere" <| fun () ->
            let store    = InMemory.create()
            let streams  = List.init 10 (fun i -> StreamId $"stream-{i}")
            let appends  =
                streams
                |> List.collect (fun sid -> List.init 5 (fun _ -> store.Append sid [mkEvent()]))
            let _        = Async.Parallel appends |> run
            for sid in streams do
                let events = store.Read sid Start |> run
                Expect.equal (List.length events) 5 $"{sid} has 5 events"
                let positions = events |> List.map (fun e -> e.Position) |> List.sort
                Expect.equal positions [0UL; 1UL; 2UL; 3UL; 4UL] $"{sid} positions are correct"
    ]

// ---------------------------------------------------------------------------
// All tests
// ---------------------------------------------------------------------------

let allTests =
    testList "Stratum.InMemory" [
        appendTests
        readTests
        existsTests
        metadataTests
        concurrencyTests
    ]
