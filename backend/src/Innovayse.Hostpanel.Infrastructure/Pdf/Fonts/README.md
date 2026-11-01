# Invoice PDF fonts

Embedded into the Infrastructure assembly and read by `Pdf/EmbeddedFontResolver.cs`. The production image
(`aspnet:9.0-alpine`) has no system fonts, so the PDF renderer can only use what is here.

| File | Source | Licence |
|---|---|---|
| `DejaVuSans.ttf`, `DejaVuSans-Bold.ttf` | [DejaVu 2.37](https://github.com/dejavu-fonts/dejavu-fonts/releases/tag/version_2_37) (`dejavu-fonts-ttf-2.37.tar.bz2`) | `LICENSE-DejaVu.txt` (Bitstream Vera, DejaVu changes public domain) |
| `NotoSansArmenian-Regular.ttf`, `NotoSansArmenian-Bold.ttf` | [Noto Sans Armenian v2.008](https://github.com/notofonts/armenian/releases/tag/NotoSansArmenian-v2.008), `unhinted/ttf` | `LICENSE-NotoSansArmenian.txt` (SIL OFL 1.1) |

Noto Sans Armenian is only a fallback for glyphs DejaVu Sans lacks, chiefly the dram sign (U+058F).

## Why the files are subsets

PDFsharp embeds only the glyphs a document uses, so the PDFs stay small either way. The files are
cut down to keep the assembly small: the full faces are about 1.5 MB, the subsets about 320 KB.

Client names and addresses are not limited to the panel's languages (en, ru, hy), so the subsets
keep Latin (including the Vietnamese letters and modifier letters such as the Ukrainian
apostrophe), Greek, Cyrillic, Armenian and Georgian, plus general punctuation and currency signs.
`Render_PrintsNamesBeyondThePanelLanguages` checks a name in each of the added scripts. A character
outside them (Arabic, Hebrew, CJK, emoji) prints as the replacement character `�` (U+FFFD), one per
character, instead of a blank: see `PdfFonts.ReplaceUncovered`. Keep U+FFFD in `U` below.

To regenerate them, use fontTools (`pip install fonttools`):

```sh
U="U+0020-007E,U+00A0-024F,U+0250-02FF,U+0300-036F,U+0370-03FF,U+0400-052F,U+0530-058F,U+10A0-10FF,U+1E00-1EFF,U+1F00-1FFF,U+2000-206F,U+20A0-20CF,U+2100-214F,U+2212,U+FB13-FB17,U+FFFD"
for f in DejaVuSans DejaVuSans-Bold; do
  pyftsubset dejavu-fonts-ttf-2.37/ttf/$f.ttf --unicodes="$U" --layout-features='kern,liga' \
    --no-hinting --desubroutinize --output-file=$f.ttf
done
for f in NotoSansArmenian-Regular NotoSansArmenian-Bold; do
  pyftsubset NotoSansArmenian/unhinted/ttf/$f.ttf --unicodes="$U" --layout-features='kern,liga' \
    --no-hinting --output-file=$f.ttf
done
```

Covering another script means widening `U` and regenerating.
