/**
 * Narrowing of a plain string (an emitted tab key, a `<select>` value) back to the TypeScript
 * enum it belongs to, so a handler never needs a cast.
 */

/**
 * Finds the member of a string enum that holds the given value.
 *
 * @param enumObject - The enum object (for example `DownloadsTab`).
 * @param value - The raw string to look up.
 * @returns The enum member whose value equals `value`, or null when there is none.
 */
export const enumValueOf = <E extends Record<string, string>>(enumObject: E, value: string): E[keyof E] | null => {
  const member = Object.values(enumObject).find((candidate) => candidate === value)
  // `Object.values` widens the members to `string`; the find above proved this one is a member.
  return (member as E[keyof E] | undefined) ?? null
}
