/**
 * How one call of a plugin route ended, as `stores/plugins.ts` hands it to the block that made it: each block keeps
 * its own result, so one failing block never touches another.
 *
 * @module types/plugincallresult
 */

/** The end of one plugin route call. */
export interface PluginCallResult {
  /** Whether the call succeeded. */
  ok: boolean
  /** The reply; null for no content or a failure. */
  data: unknown
  /**
   * The sentence of a refusal, from the API's `detail` (or the client's one "no answer" sentence when nothing
   * answered); empty on success and when {@link unavailable}.
   */
  error: string
  /** Field name to sentence, when the refusal named fields. */
  fieldErrors: Record<string, string>
  /**
   * The block named a route the page cannot call: a parameter the page's address does not carry, or a method the
   * host does not call. Nothing was sent and there is no sentence to show; the block renders nothing. The API
   * refuses such a view at install (`PLUGIN_VIEW_ROUTE_UNDECLARED`), so this is the client's guard, not a flow.
   */
  unavailable: boolean
}
