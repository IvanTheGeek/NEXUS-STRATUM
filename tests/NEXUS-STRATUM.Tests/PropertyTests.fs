module Stratum.Tests.PropertyTests

open System
open Expecto
open Hedgehog
open Stratum

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let private run a = Async.RunSynchronously a

let private mkEvent () : EventToAppend =
    { EventId       = Guid.CreateVersion7()
      CorrelationId = Guid.NewGuid()
      CausationId   = Guid.NewGuid()
      Data          = [||] }

// ---------------------------------------------------------------------------
// Properties
// ---------------------------------------------------------------------------

let allPropertyTests =
    testList "Stratum.InMemory.Properties" [

        testCase "batch of N events gets positions 0..N-1" <| fun () ->
            Property.checkBool <| property {
                let! n      = Gen.int32 (Range.linear 1 30)
                let store   = InMemory.create()
                let events  = List.init n (fun _ -> mkEvent())
                let _       = store.Append (StreamId "s") events |> run
                let stored  = store.Read (StreamId "s") Start    |> run
                let actual  = stored |> List.map (fun e -> e.Position)
                let expected = List.init n uint64
                return actual = expected
            }

        testCase "N sequential single-event appends give positions 0..N-1" <| fun () ->
            Property.checkBool <| property {
                let! n     = Gen.int32 (Range.linear 1 20)
                let store  = InMemory.create()
                let sid    = StreamId "s"
                // List.init evaluates eagerly — side effects run, result discarded
                let _      = List.init n (fun _ -> store.Append sid [mkEvent()] |> run)
                let stored    = store.Read sid Start |> run
                let positions = stored |> List.map (fun e -> e.Position)
                let expected  = List.init n uint64
                return positions = expected
            }

        testCase "byte data round-trips without corruption" <| fun () ->
            Property.checkBool <| property {
                let! data  = Gen.list (Range.linear 0 64) (Gen.byte (Range.linear 0uy 255uy))
                              |> Gen.map List.toArray
                let store  = InMemory.create()
                let evt    = { EventId = Guid.CreateVersion7(); CorrelationId = Guid.NewGuid(); CausationId = Guid.NewGuid(); Data = data }
                let _      = store.Append (StreamId "s") [evt] |> run
                let stored = store.Read (StreamId "s") Start   |> run
                return stored.[0].Data = data
            }

        testCase "Read At n returns exactly events with position >= n" <| fun () ->
            Property.checkBool <| property {
                let! total  = Gen.int32 (Range.linear 2 20)
                let! fromN  = Gen.int32 (Range.linear 0 (total - 1))
                let store   = InMemory.create()
                let sid     = StreamId "s"
                let _       = store.Append sid (List.init total (fun _ -> mkEvent())) |> run
                let events  = store.Read sid (At (uint64 fromN)) |> run
                let positions = events |> List.map (fun e -> e.Position)
                let expected  = List.init (total - fromN) (fun i -> uint64 (fromN + i))
                return positions = expected
            }

        testCase "Exists is consistent with Append — true iff at least one event appended" <| fun () ->
            Property.checkBool <| property {
                let! n    = Gen.int32 (Range.linear 1 10)
                let store = InMemory.create()
                let sid   = StreamId "s"
                let before = store.Exists sid |> run
                let _      = store.Append sid (List.init n (fun _ -> mkEvent())) |> run
                let after  = store.Exists sid |> run
                return (not before) && after
            }
    ]
