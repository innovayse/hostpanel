<script setup lang="ts">
/**
 * "You are here" trail of a full-page settings form: linked ancestors, then the current page.
 *
 * Every item but the last is a link; the last is plain text and marks the current page for
 * assistive technology.
 */
import { useI18n } from 'vue-i18n'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { Icon } from '@/lib/icons'

const { t } = useI18n()

/** One step of the trail. */
interface BreadcrumbItem {
  /** Visible caption (already translated). */
  label: string
  /** Router destination; omitted for the current page. */
  to?: string
}

/** Props for SettingsBreadcrumb. */
defineProps<{
  /** The steps, outermost first; the last one is the current page. */
  items: BreadcrumbItem[]
}>()
</script>

<template>
  <nav :aria-label="t('shared.youAreHere')" class="mb-6" data-testid="settings-breadcrumb">
    <UiStack direction="row" align="center" gap="xs">
      <template v-for="(item, index) in items" :key="item.to ?? item.label">
        <UiIcon v-if="index > 0" :markup="Icon.Chevron" size="sm" class="text-text-muted" />
        <UiLink v-if="item.to" :to="item.to" variant="link">
          {{ item.label }}
        </UiLink>
        <UiText v-else tone="primary" size="lg" class="font-medium" aria-current="page">
          {{ item.label }}
        </UiText>
      </template>
    </UiStack>
  </nav>
</template>
