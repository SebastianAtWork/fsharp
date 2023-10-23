﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.Composition
open System.Threading
open System.Threading.Tasks

open FSharp.Compiler
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Symbols
open FSharp.Compiler.Text
open FSharp.Compiler.Syntax

open Microsoft.CodeAnalysis.Text
open Microsoft.CodeAnalysis.CodeRefactorings
open Microsoft.CodeAnalysis.CodeActions
open CancellableTasks
open System.Diagnostics

[<ExportCodeRefactoringProvider(FSharpConstants.FSharpLanguageName, Name = "RemoveExplicitReturnType"); Shared>]
type internal RemoveExplicitReturnType [<ImportingConstructor>] () =
    inherit CodeRefactoringProvider()

    static member isValidMethodWithoutTypeAnnotation
        (funcOrValue: FSharpMemberOrFunctionOrValue)
        (symbolUse: FSharpSymbolUse)
        (parseFileResults: FSharpParseFileResults)
        =
        let returnTypeHintAlreadyPresent =
            parseFileResults.TryRangeOfReturnTypeHint(symbolUse.Range.Start, false)
            |> Option.isNone

        let isLambdaIfFunction =
            funcOrValue.IsFunction
            && parseFileResults.IsBindingALambdaAtPosition symbolUse.Range.Start

        (not funcOrValue.IsValue || not isLambdaIfFunction)
        && returnTypeHintAlreadyPresent

    static member refactor (context: CodeRefactoringContext) (memberFunc: FSharpMemberOrFunctionOrValue) =
        let title = SR.RemoveExplicitReturnTypeAnnotation()

        let getChangedText (sourceText: SourceText) =
            let inferredType = memberFunc.ReturnParameter.Type.TypeDefinition.DisplayName
            inferredType
            let rangeOfReturnType = memberFunc.ReturnParameter.DeclarationLocation

            let textSpan = RoslynHelpers.FSharpRangeToTextSpan(sourceText, rangeOfReturnType)
            let textChange = TextChange(textSpan, $"")

            sourceText.WithChanges(textChange)

        let codeActionFunc =
            (fun (cancellationToken: CancellationToken) ->
                task {
                    let! sourceText = context.Document.GetTextAsync(cancellationToken)
                    let changedText = getChangedText sourceText

                    let newDocument = context.Document.WithText(changedText)
                    return newDocument
                })

        let codeAction = CodeAction.Create(title, codeActionFunc, title)

        do context.RegisterRefactoring(codeAction)

    override _.ComputeRefactoringsAsync context =
        backgroundTask {
            let document = context.Document
            let position = context.Span.Start
            let! sourceText = document.GetTextAsync()
            let textLine = sourceText.Lines.GetLineFromPosition position
            let textLinePos = sourceText.Lines.GetLinePosition position
            let fcsTextLineNumber = Line.fromZ textLinePos.Line

            let! ct = Async.CancellationToken

            let! lexerSymbol =
                document.TryFindFSharpLexerSymbolAsync(position, SymbolLookupKind.Greedy, false, false, nameof (RemoveExplicitReturnType))
                |> CancellableTask.start ct

            let! (parseFileResults, checkFileResults) =
                document.GetFSharpParseAndCheckResultsAsync(nameof (RemoveExplicitReturnType))
                |> CancellableTask.start ct

            let res =
                lexerSymbol
                |> Option.bind (fun lexer ->
                    checkFileResults.GetSymbolUseAtLocation(
                        fcsTextLineNumber,
                        lexer.Ident.idRange.EndColumn,
                        textLine.ToString(),
                        lexer.FullIsland
                    ))
                |> Option.bind (fun symbolUse ->
                    match symbolUse.Symbol with
                    | :? FSharpMemberOrFunctionOrValue as v when
                        RemoveExplicitReturnType.isValidMethodWithoutTypeAnnotation v symbolUse parseFileResults
                        ->
                        Some(v)
                    | _ -> None)
                |> Option.map (fun (memberFunc) -> RemoveExplicitReturnType.refactor context memberFunc)

            return res
        }
