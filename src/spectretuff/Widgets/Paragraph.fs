namespace SpectreTuff.Widgets

open Spectre.Tui
open System.Collections.Generic

[<AutoOpen>]
module Paragraphs =

  let paragraph (lines: TextLine seq) =
    Paragraph (List<TextLine> lines)

  let ofString text =
    ParagraphExtensions.FromString text

  let ofMarkup text =
    ParagraphExtensions.FromMarkup text

  let withStyle style (paragraph: Paragraph) =
    paragraph.Style <- style
    paragraph

  let withHorizontalAlignment alignment (paragraph: Paragraph) =
    paragraph.Alignment <- alignment
    paragraph

  let withVerticalAlignment alignment (paragraph: Paragraph) =
    paragraph.VerticalAlignment <- alignment
    paragraph

  let withOverflow overflow (paragraph: Paragraph) =
    paragraph.Overflow <- overflow
    paragraph
