/**
 * The blocks a screen schema is made of, in reading order.
 *
 * Every schema kind but the tab strip layouts is a fixed arrangement of the same blocks (tiles, a form, cards, a toolbar,
 * a chart, a table), and a composite schema lists them itself. Turning each kind into that one list lets `UiSkeletonOf`
 * draw every screen by walking the blocks, and lets the schema parity checks count them, with one definition of what a
 * screen holds.
 */
import { SkeletonKind, SkeletonPartKind } from '@/utils/ui/skeletonSchema'
import type { SkeletonPart, SkeletonSchema } from '@/utils/ui/skeletonSchema'

/**
 * The blocks of a screen, top to bottom, under its header.
 *
 * @param schema - The schema of a screen.
 * @returns Its blocks; empty for a tab strip layout, whose tabs are not blocks of its own.
 */
export const partsOf = (schema: SkeletonSchema): readonly SkeletonPart[] => {
  switch (schema.kind) {
    case SkeletonKind.Form:
      return [{ kind: SkeletonPartKind.Form, columns: schema.columns }]
    case SkeletonKind.Detail:
      return [{ kind: SkeletonPartKind.Cards, columns: schema.columns, ratio: schema.ratio }]
    case SkeletonKind.List:
      return [
        ...(schema.toolbar === undefined ? [] : [{ kind: SkeletonPartKind.Toolbar, toolbar: schema.toolbar } as const]),
        ...(schema.stats === undefined ? [] : [{ kind: SkeletonPartKind.Stats, stats: schema.stats, columns: schema.statColumns } as const]),
        { kind: SkeletonPartKind.Table, table: schema.table },
      ]
    case SkeletonKind.Report:
      return [
        ...(schema.filters === undefined ? [] : [{ kind: SkeletonPartKind.Form, columns: [[{ key: 'filters', fields: schema.filters }]] } as const]),
        ...(schema.stats === undefined ? [] : [{ kind: SkeletonPartKind.Stats, stats: schema.stats, columns: schema.statColumns } as const]),
        ...(schema.map === true ? [{ kind: SkeletonPartKind.Map } as const] : []),
        ...(schema.alert === true ? [{ kind: SkeletonPartKind.Alert } as const] : []),
        ...(schema.summary === true ? [{ kind: SkeletonPartKind.Lines } as const] : []),
        ...(schema.chart === undefined ? [] : [{ kind: SkeletonPartKind.Chart, chart: schema.chart } as const]),
        ...(schema.table === undefined ? [] : [{ kind: SkeletonPartKind.Table, table: schema.table } as const]),
        ...(schema.closing === true ? [{ kind: SkeletonPartKind.Lines } as const] : []),
        ...(schema.note === undefined ? [] : [{ kind: SkeletonPartKind.Note } as const]),
      ]
    case SkeletonKind.Dashboard:
      return [
        { kind: SkeletonPartKind.Stats, stats: schema.stats, columns: schema.statColumns },
        ...schema.panels,
      ]
    case SkeletonKind.Composite:
      return schema.parts
    default:
      return []
  }
}
