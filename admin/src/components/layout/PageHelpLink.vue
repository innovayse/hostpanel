<template>
  <UiLink
    v-if="url"
    :href="url"
    external
    class="inline-flex items-center gap-1.5 text-sm text-text-secondary hover:text-text-primary"
    data-testid="page-help-link"
  >{{ t('common.help') }}</UiLink>
</template>

<script setup lang="ts">
/**
 * Help link shown on screens that have a documentation guide mapped.
 * Renders nothing when the url is null (docsUrl not configured or no guide for this route).
 */
import { useI18n } from 'vue-i18n'
import UiLink from '@/components/ui/UiLink.vue'

defineProps<{
  /** Full URL of the documentation guide, or null to hide the link. */
  url: string | null
}>()

const { t } = useI18n()
</script>
