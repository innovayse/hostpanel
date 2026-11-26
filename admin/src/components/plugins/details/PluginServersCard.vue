<script setup lang="ts">
/**
 * The servers a panel plugin serves, on the Settings tab of the plugin details page: every server stored on a module the plugin
 * declares, each with its connection status, the time of its last connection test and a link to the Servers page, where its
 * connection is configured and tested. With no matching server the card says so and links to the Servers page, where one can be
 * added. The card only shows state and links out; it loads nothing and edits nothing, and it names no particular panel: the modules
 * come from the plugin's manifest.
 */
import { useI18n } from 'vue-i18n'
import type { RouteLocationRaw } from 'vue-router'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiEmptyState from '@/components/ui/UiEmptyState.vue'
import UiHeading from '@/components/ui/UiHeading.vue'
import UiIcon from '@/components/ui/UiIcon.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import { Icon } from '@/lib/icons'
import { formatDateTime, formatRelativeFromNow } from '@/lib/format'
import { AppRoute } from '@/lib/routes'
import { SERVER_STATE_KEYS, SERVER_STATE_TONES, serverState } from '@/lib/servers'
import type { ServerOptionDto } from '@/types/server'
import { UiCardPadding } from '@/utils/ui/card'
import { UiSize } from '@/utils/ui/size'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'
import { UiHeadingVariant, UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'
import type { UiTone } from '@/utils/ui/tone'

/** Translation function and the active locale (for the relative time). */
const { t, locale } = useI18n()

/** Props for PluginServersCard. */
defineProps<{
  /** The server modules the plugin declares. */
  modules: string[]
  /** The servers stored on one of those modules, in the order to show them. */
  servers: ServerOptionDto[]
}>()

/**
 * Where a server is opened: the Servers page narrowed to the server's name, where its connection is configured and tested.
 *
 * @param server - The server.
 * @returns The route of the Servers page with the server's name as the search.
 */
const serverTarget = (server: ServerOptionDto): RouteLocationRaw => ({ path: AppRoute.Servers, query: { search: server.name } })

/**
 * Tone and caption key of a server's last known connection state.
 *
 * @param server - The server.
 * @returns The badge tone and the locale key of the caption.
 */
const stateOf = (server: ServerOptionDto): { tone: UiTone; key: string } => {
  const current = serverState(server)
  return { tone: SERVER_STATE_TONES[current], key: SERVER_STATE_KEYS[current] }
}
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" data-testid="plugin-servers-card">
    <UiStack :gap="UiGap.Sm">
      <UiHeading :level="2" :variant="UiHeadingVariant.Section">{{ t('pluginDetails.servers.heading') }}</UiHeading>
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm" data-testid="plugin-servers-modules">
        {{ t('pluginDetails.servers.description', { modules: modules.join(', ') }) }}
      </UiText>
      <UiStack v-if="servers.length > 0" :gap="UiGap.Sm" data-testid="plugin-servers-list">
        <UiStack
          v-for="server in servers"
          :key="server.id"
          :direction="UiDirection.Row"
          :justify="UiJustify.Between"
          :align="UiAlignItems.Start"
          :gap="UiGap.Sm"
          wrap
          :data-testid="`plugin-server-${server.id}`"
        >
          <UiStack :gap="UiGap.Xs" class="min-w-0">
            <UiLink :to="serverTarget(server)" class="min-w-0 break-words font-medium" :data-testid="`plugin-server-link-${server.id}`">
              {{ server.name }}
            </UiLink>
            <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="break-all font-mono">{{ server.hostname }}</UiText>
          </UiStack>
          <UiStack :gap="UiGap.Xs">
            <UiBadge :tone="stateOf(server).tone" dot :data-testid="`plugin-server-state-${server.id}`">
              {{ t(stateOf(server).key) }}
            </UiBadge>
            <UiTooltip v-if="server.lastTestedAt" :text="formatDateTime(server.lastTestedAt)">
              <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`plugin-server-tested-${server.id}`">
                {{ t('pluginDetails.servers.lastTested', { when: formatRelativeFromNow(server.lastTestedAt, locale) }) }}
              </UiText>
            </UiTooltip>
            <UiText v-else :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`plugin-server-tested-${server.id}`">
              {{ t('pluginDetails.servers.neverTested') }}
            </UiText>
          </UiStack>
        </UiStack>
      </UiStack>
      <UiEmptyState v-else compact :title="t('pluginDetails.servers.empty')" data-testid="plugin-servers-empty">
        <template #icon>
          <UiIcon :markup="Icon.Server" :size="UiSize.Md" />
        </template>
        <template #actions>
          <UiLink :to="AppRoute.Servers" data-testid="plugin-servers-open">{{ t('pluginDetails.servers.openServers') }}</UiLink>
        </template>
      </UiEmptyState>
    </UiStack>
  </UiCard>
</template>
