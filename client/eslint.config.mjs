// @ts-check
import withNuxt from './.nuxt/eslint.config.mjs'

export default withNuxt(
  // Portal templates live outside components/, so Nuxt's own exemption for
  // directory-prefixed component names does not reach them. The directory
  // already namespaces these files (templates/aurora/sections/Hero.vue is
  // never ambiguous), and the registry imports them by path rather than by
  // auto-imported name, so the multi-word rule buys nothing here.
  {
    files: ['templates/**/*.vue'],
    rules: {
      'vue/multi-word-component-names': 'off',
    },
  },
)
