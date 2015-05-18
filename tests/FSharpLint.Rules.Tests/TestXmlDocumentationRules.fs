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

module TestXmlDocumentationRules

open NUnit.Framework
open FSharpLint.Rules.XmlDocumentation
open FSharpLint.Framework.Configuration
open FSharpLint.Framework.LoadVisitors

let config name =
    Map.ofList
        [
            (AnalyserName,
                {
                    Rules = Map.ofList
                        [
                            ("ExceptionDefinitionHeader",
                                {
                                    Settings = Map.ofList
                                        [
                                            ("Enabled", Enabled("ExceptionDefinitionHeader" = name))
                                        ]
                                });
                            ("TypeDefinitionHeader",
                                {
                                    Settings = Map.ofList
                                        [
                                            ("Enabled", Enabled("TypeDefinitionHeader" = name))
                                        ]
                                });
                            ("MemberDefinitionHeader",
                                {
                                    Settings = Map.ofList
                                        [
                                            ("Enabled", Enabled("MemberDefinitionHeader" = name))
                                        ]
                                });
                        ]
                    Settings = Map.ofList []
                })
            ]

[<TestFixture>]
type TestNameConventionRulesException() =
    inherit TestRuleBase.TestRuleBase(Ast(visitor), config "ExceptionDefinitionHeader")

    [<Test>]
    member this.ExceptionWithDoubleDashComment() =
        this.Parse """
module Program

// Some exception.
exception SomeException of string"""

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 0))

    [<Test>]
    member this.ExceptionWithDoubleDashCommentSuppressed() =
        this.Parse """
[<System.Diagnostics.CodeAnalysis.SuppressMessage("XmlDocumentation", "ExceptionDefinitionHeader")>]
module Program

// Some exception.
exception SomeException of string"""

        Assert.IsTrue(this.NoErrorsExist)

    [<Test>]
    member this.ExceptionWithMultilineComment() =
        this.Parse """
module Program

(* Some exception. *)
exception SomeException of string"""

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 0))

    [<Test>]
    member this.ExceptionWithXmlComment() =
        this.Parse """
module Program

/// Some exception.
exception SomeException of string"""

        Assert.IsTrue(this.NoErrorsExist)

    [<Test>]
    member this.ExceptionNoComment() =
        this.Parse """
module Program

exception SomeException of string"""

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(4, 0))

    [<Test>]
    member this.ExceptionWithEmptyXmlComment() =
        this.Parse """
module Program
///
exception SomeException of string"""

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(4, 0))

[<TestFixture>]
type TestNameConventionRulesType() =
    inherit TestRuleBase.TestRuleBase(Ast(visitor), config "TypeDefinitionHeader")

    [<Test>]
    member this.TypeNoComment() =
        this.Parse """

type IsAType =
    /// this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(3, 5))

    [<Test>]
    member this.TypeWithDoubleDashComment() =
        this.Parse """
// this is a type
type IsAType =
    /// this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(3, 5))

    [<Test>]
    member this.TypeWithDoubleDashCommentSuppressed() =
        this.Parse """
[<System.Diagnostics.CodeAnalysis.SuppressMessage("XmlDocumentation", "TypeDefinitionHeader")>]
// this is a type
type IsAType =
    /// this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.NoErrorsExist)

    [<Test>]
    member this.TypeWithWhitespaceXmlComment() =
        this.Parse ("""
/// """ + "\t" + """
type IsAType =
    /// this is a member
    member this.Test = true
        """)

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(3, 5))

    [<Test>]
    member this.TypeWithMultilineComment() =
        this.Parse """
(* This is a type *)
type IsAType =
    /// this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(3, 5))

    [<Test>]
    member this.TypeWithXmlComment() =
        this.Parse """
/// This is a type
///
/// third line is still ok
type IsAType =
    /// this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.NoErrorsExist)


[<TestFixture>]
type TestNameConventionRulesMember() =
    inherit TestRuleBase.TestRuleBase(Ast(visitor), config "MemberDefinitionHeader")

    [<Test>]
    member this.MemberNoComment() =
        this.Parse """
/// this is a type
type IsAType =

    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 11), this.ErrorMsg)

    [<Test>]
    member this.MemberWithDoubleDashComment() =
        this.Parse """
/// this is a type
type IsAType =
    // this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 11))

    [<Test>]
    member this.MemberWithDoubleDashCommentSuppressed() =
        this.Parse """
[<System.Diagnostics.CodeAnalysis.SuppressMessage("XmlDocumentation", "MemberDefinitionHeader")>]
/// this is a type
type IsAType =
    // this is a member
    member this.Test = true
        """

        Assert.IsTrue(this.NoErrorsExist)

    [<Test>]
    member this.MemberWithWhitespaceXmlComment() =
        this.Parse ("""
/// This is a type
type IsAType =
    /// """ + "\t" + """
    member this.Test = true
        """)

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 11))

    [<Test>]
    member this.MemberWithMultilineComment() =
        this.Parse """
/// This is a type
type IsAType =
    (* This is a type *)
    member this.Test = true
        """

        Assert.IsTrue(this.ErrorsExist)
        Assert.IsTrue(this.ErrorExistsAt(5, 11))

    [<Test>]
    member this.MemberWithXmlComment() =
        this.Parse """
/// This is a type
type IsAType =
    /// This is a member
    ///
    /// third line is still ok
    member this.Test = true
        """

        Assert.IsTrue(this.NoErrorsExist)