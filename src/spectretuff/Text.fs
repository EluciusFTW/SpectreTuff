module SpectreTuff.Text

open Spectre.Tui
open System.Collections.Generic

let span (content: string) =
  TextSpan content

let styledSpan style (content: string) =
  TextSpan(content, style)

let ansiSpan (content: string) =
  TextSpan.Ansi content

let line (spans: TextSpan seq) =
  TextLine spans

let text (lines: TextLine seq) =
  Text(List<TextLine> lines)
