<script setup lang="ts">
/**
 * VersionBanner: the notice above every page of an older release and of the development docs.
 *
 * It says which version the reader is on and links to the same page in the latest docs (the config
 * writes that address into the page data as `docsLatestUrl`, because only the build knows whether the
 * page still exists there). Nothing is rendered for the latest release or an unversioned build.
 */
import { useData } from 'vitepress'
import { computed } from 'vue'

const { theme, page } = useData()

/** Version settings the config puts in the theme data. */
const settings = computed(() => theme.value.docsVersion as { kind: string; label: string; latestUrl: string } | undefined)

/** Address of this page in the latest docs, or the latest home. */
const latestHref = computed<string>(() => (page.value.frontmatter.docsLatestUrl as string | undefined) || settings.value?.latestUrl || '')

/** Whether the banner is shown. */
const visible = computed<boolean>(() => settings.value?.kind === 'release' || settings.value?.kind === 'next')
</script>

<template>
  <div v-if="visible" class="version-banner" role="note">
    <span>
      <template v-if="settings?.kind === 'next'">
        This is the documentation of the next, unreleased version.
      </template>
      <template v-else>
        This is the documentation of {{ settings?.label }}, not the latest release.
      </template>
      <a :href="latestHref">Read the latest version</a>
    </span>
  </div>
</template>

<style scoped>
/*
 * The banner is fixed above the top bar and its height is the `--vp-layout-top-height` the config writes into
 * the page head for an older release and for next (see `versionBannerStyle` in config.ts). The default theme
 * reads that variable to move the top bar, the sidebar and the content below the banner; without it the
 * fixed top bar lies over the banner. The tint is laid over the page background because the content
 * scrolls under the banner and the tint alone is see-through.
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
