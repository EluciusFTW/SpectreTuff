namespace SpectreTuff.Widgets

open Spectre.Tui
open System.Collections.Generic
open SpectreTuff

[<AutoOpen>]
module Paragraphs =

  let paragraph (lines: TextLine seq) =
    Paragraph (List<TextLine> lines)

  let ofString text =
    ParagraphExtensions.FromString text

  let ofMarkup text =
    ParagraphExtensions.FromMarkup text

  let withLook look (paragraph: Paragraph) =
    paragraph.Style <- Look.toStyle look
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
