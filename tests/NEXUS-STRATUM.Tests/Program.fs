module Stratum.Tests.Program

open Expecto

[<EntryPoint>]
let main args =
    let all =
        testList "all" [
            Tests.allTests
            PropertyTests.allPropertyTests
        ]
    runTestsWithCLIArgs [] args all
