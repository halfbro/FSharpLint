﻿namespace FSharpLint.Framework

/// Provides functionality to parse F# files using `FSharp.Compiler.Service`.
module ParseFile = 

    open System.IO
    open System.Reflection
    open FSharpLint.Framework
    open FSharpLint.Framework.Configuration
    open Microsoft.FSharp.Compiler
    open Microsoft.FSharp.Compiler.Ast
    open Microsoft.FSharp.Compiler.SourceCodeServices
    open Utilities

    /// Information for a file to be linted that is given to the analysers.
    [<NoEquality; NoComparison>]
    type FileParseInfo =
        { /// Contents of the file.
          Text: string

          /// File represented as an AST.
          Ast: ParsedInput

          /// Optional results of inferring the types on the AST (allows for a more accurate lint).
          TypeCheckResults: FSharpCheckFileResults option

          /// Path to the file.
          File: string }
          
    [<NoComparison>]
    type ParseFileFailure =
        | FailedToParseFile of FSharpErrorInfo []
        | AbortedTypeCheck
        
    [<NoComparison>]
    type ParseFileResult<'t> =
        | Failed of ParseFileFailure
        | Success of 't
        
    let private parse configuration file source (checker:FSharpChecker, options) =
        let parseResults = 
            checker.ParseFile(file, source, options |> checker.GetParsingOptionsFromProjectOptions |> fst)
            |> Async.RunSynchronously

        let typeCheckFile () =
            let results = 
                checker.CheckFileInProject(parseResults, file, 0, source, options) 
                |> Async.RunSynchronously

            match results with
            | FSharpCheckFileAnswer.Succeeded(x) -> Success(Some(x))
            | FSharpCheckFileAnswer.Aborted -> Failed(AbortedTypeCheck)

        match parseResults.ParseTree with
        | Some(parseTree) -> 
            match typeCheckFile() with
            | Success(typeCheckResults) ->
                { Text = source
                  Ast = parseTree
                  TypeCheckResults = typeCheckResults
                  File = file } |> Success
            | Failed(_) -> Failed(AbortedTypeCheck)
        | None -> Failed(FailedToParseFile(parseResults.Errors))
        
    // See: https://github.com/fsharp/FSharp.Compiler.Service/issues/847.
    let private dotnetCoreReferences () =
        let fsharpCoreDir = Path.GetDirectoryName(typeof<FSharp.Collections.List<_>>.Assembly.Location)
        let runtimeDir = Path.GetDirectoryName(typeof<System.Object>.Assembly.Location)

        [ fsharpCoreDir </> "FSharp.Core.dll"
          runtimeDir </> "mscorlib.dll"
          runtimeDir </> "System.Console.dll"
          runtimeDir </> "System.Runtime.dll"
          runtimeDir </> "System.Private.CoreLib.dll"
          runtimeDir </> "System.ObjectModel.dll"
          runtimeDir </> "System.IO.dll"
          runtimeDir </> "System.Linq.dll"
          runtimeDir </> "System.Net.Requests.dll"
          runtimeDir </> "System.Runtime.Numerics.dll"
          runtimeDir </> "System.Threading.Tasks.dll"
          
          typeof<System.Console>.Assembly.Location
          typeof<System.ComponentModel.DefaultValueAttribute>.Assembly.Location
          typeof<System.ComponentModel.PropertyChangedEventArgs>.Assembly.Location
          typeof<System.IO.BufferedStream>.Assembly.Location
          typeof<System.Linq.Enumerable>.Assembly.Location
          typeof<System.Net.WebRequest>.Assembly.Location
          typeof<System.Numerics.BigInteger>.Assembly.Location
          typeof<System.Threading.Tasks.TaskExtensions>.Assembly.Location ]
        |> List.distinct
        |> List.filter File.Exists
        |> List.distinctBy Path.GetFileName
        |> List.map (fun location -> "-r:" + location)
        
    let getProjectOptionsFromScript (checker:FSharpChecker) file source =
        #if NETSTANDARD2_0
        let assumeDotNetFramework = false
        #else
        let assumeDotNetFramework = true
        #endif

        let (options, _diagnostics) = 
            checker.GetProjectOptionsFromScript(file, source, assumeDotNetFramework = assumeDotNetFramework) 
            |> Async.RunSynchronously

        let otherOptions =
            if assumeDotNetFramework then options.OtherOptions
            else 
                [| yield! options.OtherOptions |> Array.filter (fun x -> not (x.StartsWith("-r:")))
                   yield! dotnetCoreReferences() |]

        { options with OtherOptions = otherOptions }

    /// Parses a file using `FSharp.Compiler.Service`.
    let parseFile file configuration (checker:FSharpChecker) projectOptions =
        let source = File.ReadAllText(file)

        let projectOptions =
            match projectOptions with
            | Some(existingOptions) -> existingOptions
            | None -> getProjectOptionsFromScript checker file source
        
        parse configuration file source (checker, projectOptions)
        
    /// Parses source code using `FSharp.Compiler.Service`.
    let parseSourceFile fileName source configuration (checker:FSharpChecker) =        
        let options = getProjectOptionsFromScript checker fileName source

        parse configuration fileName source (checker, options)
        
    let parseSource source configuration (checker:FSharpChecker) = 
        parseSourceFile "test.fsx" source configuration checker