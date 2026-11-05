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
    <template v-if="settings?.kind === 'next'">
      This is the documentation of the next, unreleased version.
    </template>
    <template v-else>
      This is the documentation of {{ settings?.label }}, not the latest release.
    </template>
    <a :href="latestHref">Read the latest version</a>
  </div>
</template>

<style scoped>
.version-banner {
  padding: 8px 16px;
  text-align: center;
  font-size: 14px;
  background: var(--vp-c-warning-soft);
  color: var(--vp-c-text-1);
}
.version-banner a {
  margin-left: 6px;
  font-weight: 600;
  color: var(--vp-c-brand-1);
  text-decoration: underline;
}
</style>
