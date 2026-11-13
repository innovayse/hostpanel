/**
 * Counting helpers behind `npm run audit` (see `audit.mjs`).
 *
 * The audit is a heuristic over source text, so it must look at code only: a comment that
 * explains why `v-html` is avoided, or a sentence such as "Rating choices: any, then the rest", is
 * not a use of `v-html` or of the `any` type. Comments are removed before those patterns run.
 */

/** Input-like element names whose opening tag should carry a placeholder. */
const INPUT_TAGS = 'input|textarea|select|UiInput|UiTextarea|UiSelect|UiNumberInput|UiNativeSelect|UiTextField'

/**
 * Removes comments from Vue or TypeScript source: HTML comments, block comments and line comments.
 *
 * A `//` directly after a colon or a quote is kept so that URLs inside strings survive.
 *
 * @param {string} text - Source text.
 * @returns {string} The text without comments.
 */
export const stripComments = (text) =>
  text
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:"'`\\])\/\/[^\n]*/g, '$1')

/**
 * Extracts the `<template>` part of a Vue file.
 *
 * @param {string} text - Vue source.
 * @returns {string} The template text, or an empty string.
 */
export const template = (text) => text.match(/<template>([\s\S]*)<\/template>/)?.[1] ?? ''

/**
 * Counts regex matches across a set of texts.
 *
 * @param {string[]} texts - Texts to scan.
 * @param {RegExp} pattern - Global pattern.
 * @returns {number} Total matches.
 */
export const countMatches = (texts, pattern) => texts.reduce((sum, text) => sum + (text.match(pattern)?.length ?? 0), 0)

/**
 * Counts input-like opening tags in one Vue file that have no placeholder.
 *
 * The tag pattern skips over quoted attribute values, so a `>` inside a binding such as
 * `v-if="items.length > 1"` does not end the tag early and hide the attributes after it.
 *
 * @param {string} vueText - Vue source.
 * @returns {number} Tags without a placeholder, a spread binding, or a non-text input type.
 */
export const countInputsWithoutPlaceholder = (vueText) => {
  const pattern = new RegExp(`<(?:${INPUT_TAGS})\\b(?:"[^"]*"|'[^']*'|[^>"'])*>`, 'g')
  const tags = stripComments(template(vueText)).match(pattern) ?? []
  return tags.filter((tag) => !/placeholder/.test(tag) && !/v-bind="/.test(tag) && !/type="(checkbox|radio|file|hidden)"/.test(tag)).length
}

/**
 * Counts `v-html` uses in comment-free Vue sources.
 *
 * @param {string[]} vueTexts - Vue sources.
 * @returns {number} Total uses.
 */
export const countVHtml = (vueTexts) => countMatches(vueTexts.map(stripComments), /v-html/g)

/**
 * Counts uses of the `any` type in comment-free sources.
 *
 * @param {string[]} texts - Vue and TypeScript sources.
 * @returns {number} Total uses.
 */
export const countAny = (texts) => countMatches(texts.map(stripComments), /:\s*any\b|\bas\s+any\b|<any>/g)

/**
 * Counts store sources that use the transport (`useApi`) in code. A store reaches the API only through its
 * api composable, so a comment that names `useApi` (to explain what a token is not) is not a use of it.
 *
 * @param {string[]} storeTexts - Sources of the files under `stores/`.
 * @returns {number} How many of them use `useApi` outside comments.
 */
export const countStoresCallingUseApi = (storeTexts) => storeTexts.filter((text) => /\buseApi\b/.test(stripComments(text))).length
