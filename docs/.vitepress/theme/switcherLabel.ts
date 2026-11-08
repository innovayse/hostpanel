/**
 * The text on the version button of the top bar. It lives apart from `.vitepress/versions.ts` because that
 * file reads from disk at build time and must not end up in the browser bundle.
 */
import { VersionKind } from './versionLinks'

/**
 * Text on the version button. The label of the build comes first because it is known when the page is
 * rendered, so the button has the same width before and after the list of versions has been read; the
 * latest docs of an unversioned build have no label and read "latest".
 *
 * @param kind - The kind of the build.
 * @param buildLabel - The label of the build, empty for an unversioned one.
 * @param listedLabel - The label the list of versions has for this build, when it has been read.
 * @returns The text of the button.
 */
export const switcherLabel = (kind: VersionKind, buildLabel: string, listedLabel?: string): string => {
  return buildLabel || listedLabel || (kind === VersionKind.Latest ? 'latest' : '')
}
