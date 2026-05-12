namespace SpectreTuff

open Spectre.Tui
open System.Collections.Generic

module Text =
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
