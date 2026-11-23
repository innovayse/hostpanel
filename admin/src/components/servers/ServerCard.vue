<script setup lang="ts">
/**
 * One server of the servers grid: its module, name, flags and address, the account load, its facts (group, datacenter,
 * active services and their monthly revenue, how the API is reached, nameservers), and the last
 * connection state (or the test running, or its outcome), with the row actions beside the name.
 *
 * While `server` is null the card draws the same layout with bars where the values go (module, name, address,
 * load, state), so the grid keeps its shape until the list has arrived; the row actions are left out, as there
 * is no server to act on. While a connection test runs, the state area is the same pair of bars, announced as busy.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiProgress from '@/components/ui/UiProgress.vue'
import UiTooltip from '@/components/ui/UiTooltip.vue'
import UiRowActions from '@/components/ui/UiRowActions.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import ServerFacts from '@/components/servers/ServerFacts.vue'
import { EMPTY_VALUE, formatDateTime, formatRelativeFromNow } from '@/lib/format'
import { SERVER_STATE_KEYS, SERVER_STATE_TONES, serverAccountsLabel, serverLoadPercent, serverState } from '@/lib/servers'
import { moduleNameOf } from '@/types/server'
import type { ServerDto, ServerModuleOptionDto, ServerTestOutcome } from '@/types/server'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextSize, UiTextTag, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'
import { UiCardPadding } from '@/utils/ui/card'
import { UiAlignItems, UiDirection, UiGap, UiJustify } from '@/utils/ui/layout'

/** Load percentage from which a server's bar turns to the warning colour. */
const LOAD_WARN_AT = 80

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Props for ServerCard. */
const props = withDefaults(defineProps<{
  /** The server to show; null while the list is still loading. */
  server?: ServerDto | null
  /** The modules the API declares, so the card names the server's module as its plugin does. */
  moduleOptions?: ServerModuleOptionDto[]
  /** Row actions of the server. */
  actions?: UiMenuAction[]
  /** True while a connection test is running for the server. */
  testing?: boolean
  /** Outcome of the last connection test, or null when there is none. */
  testOutcome?: ServerTestOutcome | null
}>(), {
  server: null,
  /**
   * Default for `moduleOptions`.
   *
   * @returns No declared modules: the card falls back to the host's label of the module.
   */
  moduleOptions: () => [],
  /**
   * Default for `actions`.
   *
   * @returns No row actions.
   */
  actions: () => [],
  testing: false,
  testOutcome: null,
})

/** True while there is no server yet: the card is drawn with placeholders. */
const loading = computed<boolean>(() => props.server === null)

/** The name of the server's module; empty while loading. */
const moduleName = computed<string>(() => (props.server === null ? '' : moduleNameOf(props.server.module, props.moduleOptions)))

/** Load of the server in percent, or null when it has no known capacity. */
const loadPercent = computed<number | null>(() => (props.server === null ? null : serverLoadPercent(props.server)))

/** Tone and caption key of the server's last known state; null while loading. */
const state = computed<{ tone: UiTone; key: string } | null>(() => {
  if (props.server === null) {
    return null
  }
  const current = serverState(props.server)
  return { tone: SERVER_STATE_TONES[current], key: SERVER_STATE_KEYS[current] }
})
</script>

<template>
  <UiCard :padding="UiCardPadding.Md" class="flex h-full flex-col" :aria-busy="loading ? 'true' : undefined" :data-testid="server ? `server-row-${server.id}` : 'server-card-loading'">
    <UiStack :gap="UiGap.Md" class="flex-1">
      <UiStack :direction="UiDirection.Row" :justify="UiJustify.Between" :align="UiAlignItems.Start" :gap="UiGap.Sm">
        <UiStack :gap="UiGap.Xs" class="min-w-0">
          <UiText v-if="server" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" class="font-semibold" :data-testid="`servers-module-${server.module}`">{{ moduleName }}</UiText>
          <UiSkeletonBar v-else class="h-3 w-16 rounded" aria-hidden="true" />
          <UiStack v-if="server" :direction="UiDirection.Row" :align="UiAlignItems.Center" :gap="UiGap.Sm" wrap>
            <UiText :tone="UiTextTone.Primary" :size="UiTextSize.Lg" class="min-w-0 break-words font-medium">{{ server.name }}</UiText>
            <UiTooltip v-if="server.limits.isDefault" :text="t('servers.servers.defaultHint', { module: moduleName })">
              <UiBadge :tone="UiTone.Warning" small data-testid="server-default-badge">{{ t('servers.servers.default') }}</UiBadge>
            </UiTooltip>
            <UiTooltip v-if="server.limits.isDisabled" :text="t('servers.servers.disabledHint')">
              <UiBadge :tone="UiTone.Neutral" small data-testid="server-disabled-badge">{{ t('servers.servers.disabled') }}</UiBadge>
            </UiTooltip>
          </UiStack>
          <UiSkeletonBar v-else class="h-5 w-40 rounded-md" aria-hidden="true" />
          <UiText v-if="server" :tone="UiTextTone.Muted" :size="UiTextSize.Sm" class="font-mono">{{ server.endpoint.ipAddress ?? EMPTY_VALUE }}</UiText>
          <UiSkeletonBar v-else class="h-3.5 w-28 rounded-md" aria-hidden="true" />
        </UiStack>
        <UiRowActions v-if="server" :actions="actions" :testid="`server-actions-${server.id}`" />
      </UiStack>

      <UiProgress
        v-if="loading || loadPercent !== null"
        :value="loadPercent ?? 0"
        :label="t('servers.servers.accountsLoad')"
        :value-text="server ? serverAccountsLabel(server) : undefined"
        :warn-at="LOAD_WARN_AT"
        :loading="loading"
      />
      <UiStack v-else-if="server" :direction="UiDirection.Row" :justify="UiJustify.Between" :gap="UiGap.Sm">
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ t('shared.accounts') }}</UiText>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ serverAccountsLabel(server) }}</UiText>
      </UiStack>

      <ServerFacts :server="server" />

      <!-- The footer is pinned to the bottom, so the cards of one row end on the same line whatever their content -->
      <UiStack :gap="UiGap.Xs" class="mt-auto">
        <UiStack
          v-if="!server || testing"
          :gap="UiGap.Xs"
          :aria-hidden="server ? undefined : 'true'"
          :role="server ? 'status' : undefined"
          :aria-busy="server ? 'true' : undefined"
          :data-testid="server ? `server-testing-${server.id}` : undefined"
        >
          <UiText v-if="server" :as="UiTextTag.Span" class="sr-only">{{ t('ui.loading') }}</UiText>
          <UiSkeletonBar class="h-5 w-20 rounded-full" />
          <UiSkeletonBar class="h-3 w-32 rounded" />
        </UiStack>
        <UiBadge
          v-else-if="testOutcome"
          :tone="testOutcome.ok ? UiTone.Success : UiTone.Danger"
          :data-testid="`server-test-result-${server.id}`"
        >
          {{ testOutcome.message }}
        </UiBadge>
        <UiStack v-else-if="state" :gap="UiGap.Xs">
          <UiBadge :tone="state.tone" dot :data-testid="`server-state-${server.id}`">
            {{ t(state.key) }}
          </UiBadge>
          <UiTooltip v-if="server.lastTestedAt" :text="formatDateTime(server.lastTestedAt)">
            <UiText :as="UiTextTag.Span" :tone="UiTextTone.Muted" :size="UiTextSize.Xs" :data-testid="`server-last-checked-${server.id}`">
              {{ t('servers.servers.lastChecked', { when: formatRelativeFromNow(server.lastTestedAt, locale) }) }}
            </UiText>
          </UiTooltip>
        </UiStack>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
