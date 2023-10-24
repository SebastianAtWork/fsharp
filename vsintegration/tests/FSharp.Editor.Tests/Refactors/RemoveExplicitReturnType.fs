﻿module FSharp.Editor.Tests.Refactors.RemoveExplicitReturnType

open Microsoft.VisualStudio.FSharp.Editor
open Xunit
open NUnit.Framework
open FSharp.Editor.Tests.Refactors.RefactorTestFramework
open Xunit
open System.Runtime.InteropServices

[<Theory>]
[<InlineData(":int")>]
[<InlineData(" :int")>]
[<InlineData(" : int")>]
[<InlineData(" :    int")>]
let ``Refactor changes something`` (toRemove: string) =
    task {

        let code =
            $"""
            let sum a b {toRemove}= a + b
            """

        use context = TestContext.CreateWithCode code
        let spanStart = code.IndexOf "sum"

        let! (_, text) = tryRefactor code spanStart context (new RemoveExplicitReturnType())

        Assert.AreNotEqual(code, text.ToString(), "")
        //Todo: actually test if there is no return type anymore?

        ()
    }

[<Fact>]
let ``Refactor changes nothing`` () =
    task {

        let code =
            $"""
            let sum a b = a + b
            """

        use context = TestContext.CreateWithCode code

        let spanStart = code.IndexOf "sum"

        let! (_, text) = tryRefactor code spanStart context (new RemoveExplicitReturnType())

        Assert.AreEqual(code, text.ToString(), "")

        ()
    }

[<Fact>]
let ``Refactor should not change anything`` () =
    task {

        let code =
            """
            type A = { X:int }
            type B = { X:int }
            
            let f (i: int) : A = { X = i }
            let sum a b = a + b
            """

        use context = TestContext.CreateWithCode code

        let spanStart = code.IndexOf "sum"

        let! (_, text) = tryRefactor code spanStart context (new RemoveExplicitReturnType())

        Assert.AreEqual(code, text.ToString(), "")

        ()
    }
