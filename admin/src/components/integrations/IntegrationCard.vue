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
import { integrationLabelText, integrationMetaOf } from '@/types/integrationmeta'
import type { IntegrationDto } from '@/types/integration'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiImage from '@/components/ui/UiImage.vue'
import UiPluginAvatar from '@/components/ui/UiPluginAvatar.vue'
import UiRowLink from '@/components/ui/UiRowLink.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { integrationConfigurePath } from '@/lib/routes'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiRowLinkLook } from '@/utils/ui/rowLink'
import { UiSize } from '@/utils/ui/size'
import { UiHeadingVariant, UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
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
const meta = computed(() => (props.integration === null ? undefined : integrationMetaOf(props.integration.slug)))

/** Body text: the plugin's own description, else the catalogue text of a built-in integration. */
const description = computed<string | undefined>(() => props.integration?.description ?? meta.value?.shortDescription)
</script>

<template>
  <UiRowLink
    :to="integration ? integrationConfigurePath(integration.slug) : undefined"
    :look="UiRowLinkLook.Block"
    :data-testid="integration ? `integration-card-${integration.slug}` : 'integration-card-loading'"
  >
    <UiCard :padding="UiCardPadding.Md" class="h-full" :aria-busy="integration ? undefined : 'true'">
      <UiStack :gap="UiGap.Md" class="h-full">
        <UiStack :direction="UiDirection.Row" :align="UiAlignItems.Start" :justify="UiJustify.Between">
          <UiSkeletonBar v-if="integration === null" class="h-12 w-12 rounded-control" aria-hidden="true" />
          <UiPluginAvatar
            v-else-if="integration.isPlugin"
            :name="integrationLabelText(integration.name)"
            :icon-url="integration.iconUrl"
            :size="UiSize.Lg"
            decorative
            data-testid="integration-plugin-avatar"
          />
          <UiStack v-else :align="UiAlignItems.Center" :justify="UiJustify.Center" class="h-12 w-12 rounded-control p-2.5" :class="meta?.color ?? 'bg-text-muted'">
            <UiImage
              v-if="meta?.logo"
              :src="meta.logo"
              :alt="integrationLabelText(integration.name)"
              class="h-full w-full object-contain brightness-0 invert"
            />
          </UiStack>
          <UiSkeletonBar v-if="integration === null" class="h-5 w-16 rounded-full" aria-hidden="true" />
          <UiStack v-else :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Xs">
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
            <UiBadge :tone="integration.isEnabled ? UiTone.Success : UiTone.Neutral" small :data-testid="`integration-status-${integration.slug}`">
              {{ integration.isEnabled ? t('shared.active') : t('shared.inactive') }}
            </UiBadge>
          </UiStack>
        </UiStack>

        <UiSkeletonBar v-if="integration === null" class="h-5 w-40 rounded-md" aria-hidden="true" />
        <UiStack v-else :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" class="min-w-0">
          <UiHeading :level="3" :variant="UiHeadingVariant.Card" class="min-w-0 truncate" :title="integrationLabelText(integration.name)" data-testid="integration-card-name">
            {{ integrationLabelText(integration.name) }}
          </UiHeading>
          <UiBadge v-if="integration.isPlugin" :tone="UiTone.Primary" small class="shrink-0" :data-testid="`integration-plugin-${integration.slug}`">
            {{ t('integrations.integrationCard.plugin') }}
          </UiBadge>
          <UiBadge
            v-if="!integration.isConfigured"
            :tone="UiTone.Warning"
            small
            class="shrink-0"
            :data-testid="`integration-unconfigured-${integration.slug}`"
          >
            {{ t('integrations.integrationCard.notConfigured') }}
          </UiBadge>
        </UiStack>

        <UiStack v-if="integration === null" :gap="UiGap.Xs" class="flex-1" aria-hidden="true">
          <UiSkeletonBar class="h-3.5 w-full rounded-md" />
          <UiSkeletonBar class="h-3.5 w-2/3 rounded-md" />
        </UiStack>
        <UiText v-else :tone="UiTextTone.Muted" :size="UiTextSize.Md" class="flex-1">
          {{ description }}
        </UiText>

        <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Sm" class="font-medium">
          {{ t('integrations.integrationCard.configure') }}
        </UiText>
      </UiStack>
    </UiCard>
  </UiRowLink>
</template>
