<script setup lang="ts">
/**
 * Right sidebar for the integration detail page.
 *
 * Displays the connection test state (in flight, the result of this session's test, the plugin's
 * last reported connection state, or not tested yet) and an optional contextual hint. `loading` keeps the card and its
 * caption and draws a badge-sized bar in place of the state while the integration is still being read.
 */
import { useI18n } from 'vue-i18n'
import { formatClock } from '@/lib/format'
import type { IntegrationConnection, IntegrationTestResult } from '@/types/integration'
import { integrationConnectionTone } from '@/lib/tones'
import UiAlert from '@/components/ui/UiAlert.vue'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiCard from '@/components/ui/UiCard.vue'
import UiSkeletonBar from '@/components/ui/UiSkeletonBar.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { MS_PER_SECOND } from '@/lib/limits'

/** Translation function and the active locale (for time wording). */
const { t, locale } = useI18n()

/** Props for IntegrationStatusSidebar. */
defineProps<{
  /** Draws a placeholder instead of the connection state while the integration is read. */
  loading?: boolean
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
}>()

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
  <UiStack gap="md" data-testid="integration-status">
    <!-- Status card -->
    <UiCard padding="md">
      <UiStack gap="sm">
        <UiText tone="muted" size="sm">
          {{ t('integrations.integrationStatusSidebar.connectionStatus') }}
        </UiText>

        <!-- Test in flight. Checked first: the store clears the previous result when a test
             starts, so without this branch the card would show "Not tested yet" for the
             duration of the probe. -->
        <UiSkeletonBar v-if="loading" class="h-5 w-28 rounded-full" aria-hidden="true" data-testid="integration-status-loading" />

        <UiStack v-else-if="testing" gap="xs" aria-busy="true" data-testid="integration-status-testing">
          <!-- The result it will become: a state badge and the probe's message line -->
          <UiSkeletonBar class="h-5 w-28 rounded-full" aria-hidden="true" />
          <UiSkeletonBar class="h-3.5 w-48 max-w-full rounded-md" aria-hidden="true" />
        </UiStack>

        <!-- In-session test result -->
        <UiStack v-else-if="testResult" gap="xs" data-testid="integration-status-result">
          <UiBadge :tone="testResult.success ? 'success' : 'danger'" dot>
            {{ testResult.success ? t('integrations.integrationStatusSidebar.connectionOk') : t('integrations.integrationStatusSidebar.connectionFailed') }}
          </UiBadge>
          <UiText tone="muted" size="sm">{{ testResult.message }}</UiText>

          <!-- What the probe actually did. Each row is shown only when its value exists, so a
               built-in integration without a URL still gets a tidy card. -->
          <UiStack v-if="endpointHost" direction="row" gap="sm">
            <UiText tone="muted" size="xs">{{ t('integrations.integrationStatusSidebar.endpoint') }}</UiText>
            <UiText tone="secondary" size="xs" class="truncate font-mono">{{ endpointHost }}</UiText>
          </UiStack>
          <UiStack v-if="testResult.testedAt" direction="row" gap="sm">
            <UiText tone="muted" size="xs">{{ t('integrations.integrationStatusSidebar.tested') }}</UiText>
            <UiText tone="secondary" size="xs">{{ formatClock(testResult.testedAt, locale) }}</UiText>
          </UiStack>
          <UiStack v-if="testResult.durationMs !== undefined" direction="row" gap="sm">
            <UiText tone="muted" size="xs">{{ t('integrations.integrationStatusSidebar.roundTrip') }}</UiText>
            <UiText tone="secondary" size="xs">{{ formatDuration(testResult.durationMs) }}</UiText>
          </UiStack>
        </UiStack>

        <!-- No test on this page yet, but the plugin reported its state (cached by the API) -->
        <UiStack v-else-if="connection" gap="xs" data-testid="integration-status-connection">
          <UiBadge :tone="integrationConnectionTone[connection.state]" dot>
            {{ t(`integrations.connectionState.${connection.state}`) }}
          </UiBadge>
          <UiText tone="muted" size="sm">{{ connection.message }}</UiText>
          <UiStack direction="row" gap="sm">
            <UiText tone="muted" size="xs">{{ t('integrations.integrationStatusSidebar.tested') }}</UiText>
            <UiText tone="secondary" size="xs">{{ formatClock(connection.checkedAt, locale) }}</UiText>
          </UiStack>
        </UiStack>

        <!-- Nothing tested on this page yet -->
        <UiStack v-else gap="xs" data-testid="integration-status-idle">
          <UiBadge tone="neutral" dot>
            {{ t('integrations.integrationStatusSidebar.notTestedYet') }}
          </UiBadge>
        </UiStack>
      </UiStack>
    </UiCard>

    <!-- Hint callout -->
    <UiAlert v-if="hint" variant="warning" data-testid="integration-status-hint">
      <UiStack gap="xs">
        <UiText tone="warning" size="sm" class="font-medium">
          {{ t('integrations.integrationStatusSidebar.setupNote') }}
        </UiText>
        <UiText tone="secondary" size="sm">{{ hint }}</UiText>
      </UiStack>
    </UiAlert>
  </UiStack>
</template>
