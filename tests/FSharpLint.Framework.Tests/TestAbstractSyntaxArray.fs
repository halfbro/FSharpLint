﻿(*
    FSharpLint, a linter for F#.
    Copyright (C) 2014 Matthew Mcveigh

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*)

module TestAbstractSyntaxArray

open System.IO
open System.Diagnostics
open FSharpLint.Framework
open FSharpLint.Framework.Ast
open FSharpLint.Framework.AbstractSyntaxArray
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.SourceCodeServices
open NUnit.Framework

[<TestFixture>]
type TestAst() =

    [<Literal>]
    let SourceFile = "../../TypeChecker.fs"

    let generateAst source =
        let checker = FSharpChecker.Create()

        let options = 
            checker.GetProjectOptionsFromScript(SourceFile, source) 
            |> Async.RunSynchronously

        let parseResults =
            checker.ParseFileInProject(SourceFile, source, options)
            |> Async.RunSynchronously
        
        match parseResults.ParseTree with
        | Some(parseTree) -> parseTree
        | None -> failwith "Failed to parse file."

    let astToExpr ast =
        let (|Module|_|) x =
            match x with
            | SynModuleOrNamespace(_, _, SynModuleDecl.DoExpr(_, app, _)::_, _, _, _, _) ->
                Some(app)
            | _ -> None

        match ast with
        | ParsedInput.ImplFile(x) -> 
            match x with 
            | ParsedImplFileInput(_, _, _, _, _, Module(app)::_, _) -> 
                app 
            | _ -> failwith "Expected at least one module or namespace."
        | _ -> failwith "Expected an implementation file."

    let astNodeName = string >> (fun x -> x.Substring(x.LastIndexOf("+") + 1))

    [<Test>]
    member __.``Flatten with right pipe adds lhs to end of function application.``() = 
        let result =
            generateAst "x |> List.map (fun x -> x)"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName

        Assert.AreEqual(["LongIdent"; "Lambda"; "Ident"], result)

    [<Test>]
    member __.``Flatten with left pipe adds rhs to end of function application.``() = 
        let result =
            generateAst "List.map (fun x -> x) <| x"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName

        Assert.AreEqual(["LongIdent"; "Lambda"; "Ident"], result)

    [<Test>]
    member __.``Flatten with right pipe adds lhs to end of function application no matter the number of arguments on rhs.``() = 
        let result =
            generateAst "x |> List.map (fun x -> x) 1"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName
            
        Assert.AreEqual(["LongIdent"; "Lambda"; "Const"; "Ident"], result)

    [<Test>]
    member __.``Flatten with binary operator on lhs of right pipe.``() = 
        let result =
            generateAst "x::[] |> List.map (fun x -> x)"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName
            
        Assert.AreEqual(["LongIdent"; "Lambda"; "App"], result)

    [<Test>]
    member __.``Flatten with function application on lhs of right pipe.``() = 
        let result =
            generateAst "(foo x) |> List.map (fun x -> x)"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName
            
        Assert.AreEqual(["LongIdent"; "Lambda"; "App"], result)

    [<Test>]
    member __.``Flatten with multiple right pipes.``() = 
        let result =
            generateAst "x |> foo |> List.map (fun x -> x)"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName
            
        Assert.AreEqual(["LongIdent"; "Lambda"; "App"], result)

    [<Test>]
    member __.``Flatten with multiple left pipes.``() = 
        let result =
            generateAst "List.map (fun x -> x) <| 1 <| x"
            |> astToExpr
            |> AstTemp.flattenFunctionApplication
            |> List.map astNodeName
            
        Assert.AreEqual(["LongIdent"; "Lambda"; "Const"; "Ident"], result)

    [<Category("Performance")>]
    [<Test>]
    member __.``Check performance of walking tree for matching hints``() = 
        let tree = File.ReadAllText SourceFile |> generateAst

        let stopwatch = Stopwatch.StartNew()

        astToArray tree |> ignore

        stopwatch.Stop()

        Assert.Less(stopwatch.ElapsedMilliseconds, 200)
        System.Console.WriteLine(sprintf "Built array in %d milliseconds." stopwatch.ElapsedMilliseconds)

    [<Test>]
    member __.``Syntax array constructed from AST in valid order.``() = 
        let tree = generateAst "List.map (fun x y -> id x) woofs"

        let (array, skipArray) = astToArray tree

        let actual = array |> Array.map (fun x -> x.Hashcode)

        let expected =
            [ (SyntaxNode.FuncApp, 0).GetHashCode()
              (SyntaxNode.Identifier, "map").GetHashCode()
              (SyntaxNode.Lambda, 0).GetHashCode()
              (SyntaxNode.LambdaArg, 0).GetHashCode()
              (SyntaxNode.Identifier, "x").GetHashCode()
              (SyntaxNode.LambdaArg, 0).GetHashCode()
              (SyntaxNode.Identifier, "y").GetHashCode()
              (SyntaxNode.LambdaBody, 0).GetHashCode()
              (SyntaxNode.FuncApp, 0).GetHashCode()
              (SyntaxNode.Identifier, "id").GetHashCode()
              (SyntaxNode.Identifier, "x").GetHashCode()
              (SyntaxNode.Identifier, "woofs").GetHashCode() ]

        Assert.AreEqual(expected, actual)
        Assert.AreEqual([11;0;8;1;0;1;0;3;2;0;0;0], skipArray)