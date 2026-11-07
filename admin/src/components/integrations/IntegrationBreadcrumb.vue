<script setup lang="ts">
/**
 * Breadcrumb of the integration pages: a link back to the integrations list, then the name of the
 * integration being configured.
 */
import { useI18n } from 'vue-i18n'
import UiLink from '@/components/ui/UiLink.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { AppRoute } from '@/lib/routes'
import { UiSize } from '@/utils/ui/size'

/** Translation function. */
const { t } = useI18n()

/** Props for IntegrationBreadcrumb. */
withDefaults(defineProps<{
  /** Name of the integration, already translated or as the API returned it. */
  name: string
  /** True for a plugin's integration, which shows its picture beside the name. */
  isPlugin?: boolean
  /** Address of the plugin's icon; null or omitted when it has none. */
  iconUrl?: string | null
}>(), {
  isPlugin: false,
  iconUrl: null,
})
</script>

<template>
  <UiStack direction="row" align="center" gap="sm" data-testid="integration-breadcrumb">
    <UiLink :to="AppRoute.Integrations" variant="link" data-testid="integration-breadcrumb-root">
      {{ t('shared.integrations') }}
    </UiLink>
    <UiText tone="muted" size="md">/</UiText>
    <UiPluginAvatar v-if="isPlugin" :name="name" :icon-url="iconUrl" :size="UiSize.Sm" data-testid="integration-breadcrumb-avatar" />
    <UiText tone="primary" size="md" class="font-medium">{{ name }}</UiText>
  </UiStack>
</template>
