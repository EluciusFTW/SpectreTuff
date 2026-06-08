[<AutoOpen>]
module SpectreTuff.Widgets.Paragraphs

open Spectre.Tui
open System.Collections.Generic
open SpectreTuff.Look

let paragraph (lines: TextLine seq) =
  Paragraph(List<TextLine> lines)

let ofString text =
  ParagraphExtensions.FromString text

let ofMarkup text =
  ParagraphExtensions.FromMarkup text

let withLook look (paragraph: Paragraph) =
  paragraph.Style <- toStyle look
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
