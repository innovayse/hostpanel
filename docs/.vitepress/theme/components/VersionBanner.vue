<script setup lang="ts">
/**
 * VersionBanner: the notice above every page of an older release and of the development docs.
 *
 * It says which version the reader is on and links to the same page in the latest docs (the config
 * writes that address into the page data as `docsLatestUrl`, because only the build knows whether the
 * page still exists there). Nothing is rendered for the latest release or an unversioned build.
 *
 * The theme renders it in the `layout-top` slot, before the top bar. The default theme moves the top
 * bar, the sidebar and the content down by `--vp-layout-top-height`, so the banner sets that variable
 * itself (see the style below) while it is on the page; no style is written into the page head and the
 * first paint is right without a script, because the banner is part of the pre-rendered page.
 */
import { useData } from 'vitepress'
import { computed } from 'vue'
import { format } from '../../i18n/ui'
import { VersionKind } from '../versionLinks'
import { useUi } from '../useUi'

const { theme, page } = useData()

/** The texts of the language of the open page. */
const { ui } = useUi()

/** Version settings the config puts in the theme data. */
const settings = computed(() => theme.value.docsVersion as { kind: VersionKind; label: string; latestUrl: string } | undefined)

/** Address of this page in the latest docs, or the latest home. */
const latestHref = computed<string>(() => (page.value.frontmatter.docsLatestUrl as string | undefined) || settings.value?.latestUrl || '')

/** Whether the banner is shown. */
const visible = computed<boolean>(() => settings.value?.kind === VersionKind.Release || settings.value?.kind === VersionKind.Next)
</script>

<template>
  <div v-if="visible" class="version-banner" role="note">
    <span>
      <template v-if="settings?.kind === VersionKind.Next">
        {{ ui.components.versionBanner.next }}
      </template>
      <template v-else>
        {{ format(ui.components.versionBanner.release, { version: settings?.label ?? '' }) }}
      </template>
      <a :href="latestHref">{{ ui.components.versionBanner.link }}</a>
    </span>
  </div>
</template>

<style>
/*
 * Height of the banner for the layout: one line of text on a wide screen, two lines up to 720px wide.
 * It is set only while the banner is on the page, so the latest release keeps the default of zero.
 */
:root:has(.version-banner) {
  --vp-layout-top-height: 40px;
}
@media (max-width: 720px) {
  :root:has(.version-banner) {
    --vp-layout-top-height: 64px;
  }
}
</style>

<style scoped>
/*
 * The banner is fixed above the top bar with the height the layout reserves for it. The tint is laid
 * over the page background because the content scrolls under the banner and the tint alone is see-through.
 */
.version-banner {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: var(--vp-z-index-layout-top);
  box-sizing: border-box;
  display: flex;
  align-items: center;
  justify-content: center;
  height: var(--vp-layout-top-height);
  padding: 0 16px;
  text-align: center;
  font-size: 14px;
  line-height: 20px;
  background: linear-gradient(var(--vp-c-warning-soft), var(--vp-c-warning-soft)), var(--vp-c-bg);
  color: var(--vp-c-text-1);
}
.version-banner a {
  margin-left: 6px;
  font-weight: 600;
  color: var(--vp-c-brand-1);
  text-decoration: underline;
}
</style>
