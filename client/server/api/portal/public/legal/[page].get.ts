/**
 * GET /api/portal/public/legal/:page
 *
 * The operator's own legal text of one page (`terms`, `privacy`, `refund`, `acceptable_use` or `cookies`) as a
 * key-to-value object holding the rows that have a value, in each language the operator wrote it in.
 *
 * The legal texts are not part of `GET /api/portal/public/settings`: each is up to 12000 characters, and that
 * object rides in every page. They are read here, one page at a time, by the legal page that shows them. The
 * backend names the rows of the page itself, so this route never chooses a key.
 *
 * Like the settings read it never fails the request: a backend that is down, or one without this endpoint,
 * yields an empty object and the page keeps its built-in text.
 *
 * @module server/api/portal/public/legal/[page].get
 */

import { RouteParamKind } from '~/types/routeparamkind'

/** One public setting row, as the backend lists it. */
interface PublicSettingRow {
  /** Setting key, e.g. `portal.legal.terms.ru`. */
  key: string
  /** Setting value. */
  value: string
}

/** Handler: the page's non-empty legal text rows as a key-to-value object; `{}` when the backend cannot answer. */
export default defineEventHandler(async (event) => {
  const page = requireRouteParam(event, 'page', RouteParamKind.Identifier)

  try {
    const rows = await internalApiCall<PublicSettingRow[]>(event, `/settings/public/legal/${page}`)

    const texts: Record<string, string> = {}
    for (const row of rows ?? []) {
      if (row.value) {
        texts[row.key] = row.value
      }
    }
    return texts
  } catch {
    // Backend unreachable, or too old to have this endpoint: the page keeps its built-in text.
    return {}
  }
})
