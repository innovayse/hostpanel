<script setup lang="ts">
/**
 * Displays a single integration as a clickable card with logo, title, description, and status,
 * marking a plugin with one small "Plugin" badge, an integration whose required fields are not all stored
 * as not configured, and a plugin that reports its connection state with a short badge of that state (the plugin's own
 * sentence is only its tooltip). The body is the built-in's catalogue text or the plugin's own description in the
 * admin's language; the title stays on one line, cut with an ellipsis (the full name is its tooltip). The card sits on a
 * category page, so it carries no badge for the category.
 *
 * Clicking the card navigates to the integration's configuration page. While `integration` is null (the list
 * is still loading) the card draws the same layout with bars where the logo, status, name and description go,
 * and is no link; the "Configure" caption is page text and stays.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { INTEGRATION_META } from '@/types/integrationmeta'
import type { IntegrationDto, IntegrationSlug } from '@/types/integration'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { integrationConfigurePath } from '@/lib/routes'
import { UiSize } from '@/utils/ui/size'
import { integrationConnectionTone } from '@/lib/tones'

/** Translation function. */
const { t } = useI18n()

/** Props for IntegrationCard. */
const props = withDefaults(defineProps<{
  /** The integration summary data from the store; null while the list is loading. */
  integration?: IntegrationDto | null
}>(), {
  integration: null,
})

/** Static metadata for this integration (logo, color, description); absent while loading and for a plugin. */
const meta = computed(() => (props.integration === null ? undefined : INTEGRATION_META[props.integration.slug as IntegrationSlug]))

/** Body text: the plugin's own description, else the catalogue text of a built-in integration. */
const description = computed<string | undefined>(() => props.integration?.description ?? meta.value?.shortDescription)
</script>

<template>
  <UiLink
    :to="integration ? integrationConfigurePath(integration.slug) : undefined"
    class="block"
    :data-testid="integration ? `integration-card-${integration.slug}` : 'integration-card-loading'"
  >
    <UiCard padding="md" class="h-full" :aria-busy="integration ? undefined : 'true'">
      <UiStack gap="md" class="h-full">
        <UiStack direction="row" align="start" justify="between">
          <UiSkeletonBar v-if="integration === null" class="h-12 w-12 rounded-control" aria-hidden="true" />
          <UiPluginAvatar
            v-else-if="integration.isPlugin"
            :name="integration.name"
            :icon-url="integration.iconUrl"
            :size="UiSize.Lg"
            data-testid="integration-plugin-avatar"
          />
          <UiStack v-else align="center" justify="center" class="h-12 w-12 rounded-control p-2.5" :class="meta?.color ?? 'bg-text-muted'">
            <UiImage
              v-if="meta?.logo"
              :src="meta.logo"
              :alt="integration.name"
              class="h-full w-full object-contain brightness-0 invert"
            />
          </UiStack>
          <UiSkeletonBar v-if="integration === null" class="h-5 w-16 rounded-full" aria-hidden="true" />
          <UiStack v-else direction="row" align="center" gap="xs">
            <!-- The plugin's own report (IPluginStatus): only the state is worded here, its sentence is the tooltip. -->
            <UiBadge
              v-if="integration.connection"
              :tone="integrationConnectionTone[integration.connection.state]"
              dot
              small
              :title="integration.connection.message"
              :data-testid="`integration-connection-${integration.slug}`"
            >
              {{ t(`integrations.connectionState.${integration.connection.state}`) }}
            </UiBadge>
            <UiBadge :tone="integration.isEnabled ? 'success' : 'neutral'" small :data-testid="`integration-status-${integration.slug}`">
              {{ integration.isEnabled ? t('shared.active') : t('shared.inactive') }}
            </UiBadge>
          </UiStack>
        </UiStack>

        <UiSkeletonBar v-if="integration === null" class="h-5 w-40 rounded-md" aria-hidden="true" />
        <UiStack v-else direction="row" align="center" gap="sm" class="min-w-0">
          <UiHeading :level="3" variant="card" class="min-w-0 truncate" :title="integration.name" data-testid="integration-card-name">
            {{ integration.name }}
          </UiHeading>
          <UiBadge v-if="integration.isPlugin" tone="primary" small class="shrink-0" :data-testid="`integration-plugin-${integration.slug}`">
            {{ t('integrations.integrationCard.plugin') }}
          </UiBadge>
          <UiBadge
            v-if="!integration.isConfigured"
            tone="warning"
            small
            class="shrink-0"
            :data-testid="`integration-unconfigured-${integration.slug}`"
          >
            {{ t('integrations.integrationCard.notConfigured') }}
          </UiBadge>
        </UiStack>

        <UiStack v-if="integration === null" gap="xs" class="flex-1" aria-hidden="true">
          <UiSkeletonBar class="h-3.5 w-full rounded-md" />
          <UiSkeletonBar class="h-3.5 w-2/3 rounded-md" />
        </UiStack>
        <UiText v-else tone="muted" size="md" class="flex-1">
          {{ description }}
        </UiText>

        <UiText tone="primary" size="sm" class="font-medium">
          {{ t('integrations.integrationCard.configure') }}
        </UiText>
      </UiStack>
    </UiCard>
  </UiLink>
</template>
