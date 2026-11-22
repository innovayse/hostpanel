/**
 * The closed value sets of servers that a Playwright fixture also reads. The file imports nothing, so a spec
 * can load it in plain Node: `types/server.ts` pulls in the translation module, which needs the Vite build.
 */

/** Where a control panel module the server form offers comes from. */
export const ServerModuleSource = {
  /** The host's own code, or an id saved servers still carry. */
  BuiltIn: 'BuiltIn',
  /** An installed plugin declares it. */
  Plugin: 'Plugin',
} as const

/** Where a control panel module comes from. */
export type ServerModuleSource = (typeof ServerModuleSource)[keyof typeof ServerModuleSource]
