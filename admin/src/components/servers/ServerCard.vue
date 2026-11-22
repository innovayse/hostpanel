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
import { moduleLabel } from '@/types/server'
import type { ServerDto, ServerTestOutcome } from '@/types/server'
import type { UiMenuAction } from '@/utils/ui/menu'
import { UiTextTag } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Load percentage from which a server's bar turns to the warning colour. */
const LOAD_WARN_AT = 80

/** Translation function and the active locale (for date formatting). */
const { t, locale } = useI18n()

/** Props for ServerCard. */
const props = withDefaults(defineProps<{
  /** The server to show; null while the list is still loading. */
  server?: ServerDto | null
  /** Row actions of the server. */
  actions?: UiMenuAction[]
  /** True while a connection test is running for the server. */
  testing?: boolean
  /** Outcome of the last connection test, or null when there is none. */
  testOutcome?: ServerTestOutcome | null
}>(), {
  server: null,
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
  <UiCard padding="md" :aria-busy="loading ? 'true' : undefined" :data-testid="server ? `server-row-${server.id}` : 'server-card-loading'">
    <UiStack gap="md">
      <UiStack direction="row" justify="between" align="start" gap="sm">
        <UiStack gap="xs" class="min-w-0">
          <UiText v-if="server" tone="muted" size="xs" class="font-semibold" :data-testid="`servers-module-${server.module}`">{{ moduleLabel(server.module) }}</UiText>
          <UiSkeletonBar v-else class="h-3 w-16 rounded" aria-hidden="true" />
          <UiStack v-if="server" direction="row" align="center" gap="sm" wrap>
            <UiText tone="primary" size="lg" class="min-w-0 break-words font-medium">{{ server.name }}</UiText>
            <UiTooltip v-if="server.limits.isDefault" :text="t('servers.servers.defaultHint', { module: moduleLabel(server.module) })">
              <UiBadge tone="warning" small data-testid="server-default-badge">{{ t('servers.servers.default') }}</UiBadge>
            </UiTooltip>
            <UiTooltip v-if="server.limits.isDisabled" :text="t('servers.servers.disabledHint')">
              <UiBadge tone="neutral" small data-testid="server-disabled-badge">{{ t('servers.servers.disabled') }}</UiBadge>
            </UiTooltip>
          </UiStack>
          <UiSkeletonBar v-else class="h-5 w-40 rounded-md" aria-hidden="true" />
          <UiText v-if="server" tone="muted" size="sm" class="font-mono">{{ server.endpoint.ipAddress ?? EMPTY_VALUE }}</UiText>
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
      <UiStack v-else-if="server" direction="row" justify="between" gap="sm">
        <UiText tone="secondary" size="sm">{{ t('shared.accounts') }}</UiText>
        <UiText tone="muted" size="sm">{{ serverAccountsLabel(server) }}</UiText>
      </UiStack>

      <ServerFacts :server="server" />

      <UiStack
        v-if="!server || testing"
        gap="xs"
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
      <UiStack v-else-if="state" gap="xs">
        <UiBadge :tone="state.tone" dot :data-testid="`server-state-${server.id}`">
          {{ t(state.key) }}
        </UiBadge>
        <UiTooltip v-if="server.lastTestedAt" :text="formatDateTime(server.lastTestedAt)">
          <UiText :as="UiTextTag.Span" tone="muted" size="xs" :data-testid="`server-last-checked-${server.id}`">
            {{ t('servers.servers.lastChecked', { when: formatRelativeFromNow(server.lastTestedAt, locale) }) }}
          </UiText>
        </UiTooltip>
      </UiStack>
    </UiStack>
  </UiCard>
</template>
