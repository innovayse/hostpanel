<script setup lang="ts">
/**
 * Body of the connection status card of the integration detail page.
 *
 * Displays the connection test state (in flight, the result of this session's test, the plugin's
 * last reported connection state, the servers of a panel plugin, or not tested yet) and an optional contextual hint. The card around it, with its caption
 * and its skeleton while the integration is still being read, is the status card of `IntegrationDetailView.schema`, which
 * `UiDetailCards` draws.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatClock } from '@/lib/format'
import type { IntegrationConnection, IntegrationTestResult } from '@/types/integration'
import type { ServerOptionDto } from '@/types/server'
import { SERVERS_GROUP_STATE_KEYS, SERVERS_GROUP_STATE_TONES, summarizeServers } from '@/lib/servers'
import { integrationConnectionTone } from '@/lib/tones'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { MS_PER_SECOND } from '@/lib/limits'
import { UiDirection, UiGap } from '@/utils/ui/layout'
import { UiTextSize, UiTextTone } from '@/utils/ui/text'
import { UiTone } from '@/utils/ui/tone'

/** Translation function and the active locale (for time wording). */
const { t, locale } = useI18n()

/** Props for IntegrationStatusSidebar. */
const props = defineProps<{
  /** Result of the most recent in-session test, or null. */
  testResult: IntegrationTestResult | null
  /** True while a connection test is in flight. */
  testing?: boolean
  /** Host the integration talks to (e.g. "gateway.example.com"), when it has one. */
  endpointHost?: string
  /** Optional hint text shown in a callout box. */
  hint?: string
  /** Last connection state a plugin reported, shown while no test ran on this page; null or absent for none. */
  connection?: IntegrationConnection | null
  /** The servers a panel plugin serves: its connection is kept per server, so they stand in for a plugin level state; absent or empty for none. */
  servers?: ServerOptionDto[]
}>()

/** How the plugin's servers stand together, or null when it has none to sum up. */
const serversSummary = computed(() => (props.servers && props.servers.length > 0 ? summarizeServers(props.servers) : null))

/**
 * Formats a round-trip duration for the detail row.
 *
 * @param ms - Duration in milliseconds.
 * @returns "812 ms" below a second, "2.4 s" above it.
 */
const formatDuration = (ms: number): string =>
  ms < MS_PER_SECOND ? `${ms} ms` : `${(ms / MS_PER_SECOND).toFixed(1)} s`
</script>

<template>
  <UiStack :gap="UiGap.Md" data-testid="integration-status">
    <!-- Status -->
    <UiStack :gap="UiGap.Sm">
      <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
        {{ t('integrations.integrationStatusSidebar.connectionStatus') }}
      </UiText>

      <!--
        Test in flight. Checked first: the store clears the previous result when a test
        starts, so without this branch the card would show "Not tested yet" for the
        duration of the probe.
      -->
      <UiStack v-if="testing" :gap="UiGap.Xs" aria-busy="true" data-testid="integration-status-testing">
        <!-- The result it will become: a state badge and the probe's message line -->
        <UiSkeletonBar class="h-5 w-28 rounded-full" aria-hidden="true" />
        <UiSkeletonBar class="h-3.5 w-48 max-w-full rounded-md" aria-hidden="true" />
      </UiStack>

      <!-- In-session test result -->
      <UiStack v-else-if="testResult" :gap="UiGap.Xs" data-testid="integration-status-result">
        <UiBadge :tone="testResult.success ? UiTone.Success : UiTone.Danger" dot>
          {{ testResult.success ? t('integrations.integrationStatusSidebar.connectionOk') : t('integrations.integrationStatusSidebar.connectionFailed') }}
        </UiBadge>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ testResult.message }}</UiText>

        <!--
          What the probe actually did. Each row is shown only when its value exists, so a
          built-in integration without a URL still gets a tidy card.
        -->
        <UiStack v-if="endpointHost" :direction="UiDirection.Row" :gap="UiGap.Sm">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('integrations.integrationStatusSidebar.endpoint') }}</UiText>
          <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Xs" class="truncate font-mono">{{ endpointHost }}</UiText>
        </UiStack>
        <UiStack v-if="testResult.testedAt" :direction="UiDirection.Row" :gap="UiGap.Sm">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('integrations.integrationStatusSidebar.tested') }}</UiText>
          <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Xs">{{ formatClock(testResult.testedAt, locale) }}</UiText>
        </UiStack>
        <UiStack v-if="testResult.durationMs !== undefined" :direction="UiDirection.Row" :gap="UiGap.Sm">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('integrations.integrationStatusSidebar.roundTrip') }}</UiText>
          <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Xs">{{ formatDuration(testResult.durationMs) }}</UiText>
        </UiStack>
      </UiStack>

      <!-- No test on this page yet, but the plugin reported its state (cached by the API) -->
      <UiStack v-else-if="connection" :gap="UiGap.Xs" data-testid="integration-status-connection">
        <UiBadge :tone="integrationConnectionTone[connection.state]" dot>
          {{ t(`integrations.connectionState.${connection.state}`) }}
        </UiBadge>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">{{ connection.message }}</UiText>
        <UiStack :direction="UiDirection.Row" :gap="UiGap.Sm">
          <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Xs">{{ t('integrations.integrationStatusSidebar.tested') }}</UiText>
          <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Xs">{{ formatClock(connection.checkedAt, locale) }}</UiText>
        </UiStack>
      </UiStack>

      <!-- A panel plugin keeps its connection per server: the card reflects its servers -->
      <UiStack v-else-if="serversSummary" :gap="UiGap.Xs" data-testid="integration-status-servers">
        <UiBadge :tone="SERVERS_GROUP_STATE_TONES[serversSummary.state]" dot data-testid="integration-status-servers-state">
          {{ t(SERVERS_GROUP_STATE_KEYS[serversSummary.state]) }}
        </UiBadge>
        <UiText :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('integrations.integrationStatusSidebar.serversOnline', { online: serversSummary.online, total: serversSummary.total }) }}
        </UiText>
        <UiText v-if="serversSummary.offline > 0" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('integrations.integrationStatusSidebar.serversOffline', { count: serversSummary.offline }) }}
        </UiText>
        <UiText v-if="serversSummary.untested > 0" :tone="UiTextTone.Muted" :size="UiTextSize.Sm">
          {{ t('integrations.integrationStatusSidebar.serversUntested', { count: serversSummary.untested }) }}
        </UiText>
      </UiStack>

      <!-- Nothing tested on this page yet -->
      <UiStack v-else :gap="UiGap.Xs" data-testid="integration-status-idle">
        <UiBadge :tone="UiTone.Neutral" dot>
          {{ t('integrations.integrationStatusSidebar.notTestedYet') }}
        </UiBadge>
      </UiStack>
    </UiStack>

    <!-- Hint callout -->
    <UiAlert v-if="hint" :variant="UiTone.Warning" data-testid="integration-status-hint">
      <UiStack :gap="UiGap.Xs">
        <UiText :tone="UiTextTone.Warning" :size="UiTextSize.Sm" class="font-medium">
          {{ t('integrations.integrationStatusSidebar.setupNote') }}
        </UiText>
        <UiText :tone="UiTextTone.Secondary" :size="UiTextSize.Sm">{{ hint }}</UiText>
      </UiStack>
    </UiAlert>
  </UiStack>
</template>
