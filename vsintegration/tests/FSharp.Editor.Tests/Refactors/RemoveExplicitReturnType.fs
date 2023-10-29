﻿module FSharp.Editor.Tests.Refactors.RemoveExplicitReturnType

open Microsoft.VisualStudio.FSharp.Editor
open Xunit
open NUnit.Framework
open FSharp.Editor.Tests.Refactors.RefactorTestFramework
open Microsoft.CodeAnalysis.Text
open Xunit
open System.Runtime.InteropServices

[<Theory>]
[<InlineData(":int")>]
[<InlineData(" :int")>]
[<InlineData(" : int")>]
[<InlineData(" :    int")>]
let ``Refactor removes explicit return type`` (toRemove: string) =
    task {
        let symbolName = "sum"

        let code =
            $"""
            let sum a b {toRemove}= a + b
            """

        use context = TestContext.CreateWithCode code
        let spanStart = code.IndexOf symbolName

        let! newDoc = tryRefactor code spanStart context (new RemoveExplicitReturnType())

        do! AssertHasNoExplicitReturnType symbolName newDoc context.CT
    }

[<Fact>]
let ``Refactor should not be suggested if theres nothing to remove`` () =
    task {

        let symbolName = "sum"

        let code =
            $"""
            let sum a b = a + b
            """

        use context = TestContext.CreateWithCode code

        let spanStart = code.IndexOf symbolName

        let! actions = tryGetRefactoringActions code spanStart context (new RemoveExplicitReturnType())

        do Assert.Empty(actions)
        ()
    }

[<Fact>]
let ``Refactor should not be suggested if it changes the meaning`` () =
    task {
        let symbolName = "sum"

        let code =
            """
            type A = { X:int }
            type B = { X:int }
            
            let f (i: int) : A = { X = i }
            let sum a b = a + b
            """

        use context = TestContext.CreateWithCode code

        let spanStart = code.IndexOf symbolName

        let! actions = tryGetRefactoringActions code spanStart context (new RemoveExplicitReturnType())

        do Assert.Empty(actions)

        ()
    }
