<script setup lang="ts">
/**
 * The demo accounts a public demo publishes, as a glass card with one row per account on the sign-in screen.
 *
 * Each row shows the role badge, the email and the password. An Admin row has a "Use" button that hands the account to
 * the parent (which fills the form; the visitor still presses Sign in). A Client account cannot sign in to the admin, so
 * its row has an "Open client portal" link to the storefront instead, and client rows are left out when the deployment
 * publishes no storefront address. Rendering nothing for an empty list is the caller's job.
 */
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import UiBadge from '@/components/ui/UiBadge.vue'
import UiButton from '@/components/ui/UiButton.vue'
import UiLink from '@/components/ui/UiLink.vue'
import UiStack from '@/components/ui/UiStack.vue'
import UiText from '@/components/ui/UiText.vue'
import { getRuntimeConfig } from '@/lib/runtimeConfig'
import { DemoAccountRole } from '@/types/demo'
import type { DemoAccount } from '@/types/demo'
import type { UiTone } from '@/utils/ui/tone'

/** Props for DemoAccountList. */
const props = defineProps<{
  /** The published demo accounts, one row each. */
  accounts: DemoAccount[]
}>()

/** Events of DemoAccountList. */
const emit = defineEmits<{
  /** Emitted with the account whose "Use" button was pressed. */
  use: [account: DemoAccount]
}>()

/** Translation function. */
const { t } = useI18n()

/** Storefront origin from runtime config; empty when the deployment does not publish it. */
const { clientBaseUrl } = getRuntimeConfig()

/** The accounts worth drawing: every Admin one, and the Client ones only when there is a portal to send them to. */
const visibleAccounts = computed<DemoAccount[]>(() =>
  props.accounts.filter(account => account.role === DemoAccountRole.Admin || clientBaseUrl !== ''),
)

/** Badge colour per role: the operator account stands out, the customer account stays neutral. */
const roleTones: Record<DemoAccountRole, UiTone> = {
  [DemoAccountRole.Admin]: 'primary',
  [DemoAccountRole.Client]: 'neutral',
}

/** Translation key of each role's label. */
const roleLabelKeys: Record<DemoAccountRole, string> = {
  [DemoAccountRole.Admin]: 'demo.login.roles.admin',
  [DemoAccountRole.Client]: 'demo.login.roles.client',
}
</script>

<template>
  <UiStack gap="sm" class="glass rounded-card border border-border p-3" data-testid="demo-login">
    <UiText size="sm" tone="secondary">{{ t('demo.login.hint') }}</UiText>
    <UiStack
      v-for="account in visibleAccounts"
      :key="`${account.role}:${account.email}`"
      direction="row"
      gap="sm"
      align="center"
      justify="between"
      class="rounded-lg bg-surface-hover px-3 py-2"
      :data-testid="`demo-login-account-${account.role}`"
    >
      <UiStack gap="xs" class="min-w-0">
        <UiBadge :tone="roleTones[account.role]" small>{{ t(roleLabelKeys[account.role]) }}</UiBadge>
        <UiText size="sm" class="truncate" data-testid="demo-login-email">
          {{ t('demo.login.email', { email: account.email }) }}
        </UiText>
        <UiText size="sm" class="truncate" data-testid="demo-login-password">
          {{ t('demo.login.password', { password: account.password }) }}
        </UiText>
      </UiStack>
      <UiButton
        v-if="account.role === DemoAccountRole.Admin"
        variant="secondary"
        size="sm"
        type="button"
        data-testid="demo-login-use"
        @click="emit('use', account)"
      >
        {{ t('demo.login.use') }}
      </UiButton>
      <UiLink
        v-else
        :href="clientBaseUrl"
        external
        class="shrink-0 text-xs font-medium underline"
        data-testid="demo-login-client-portal"
      >{{ t('demo.login.openClientPortal') }}</UiLink>
    </UiStack>
  </UiStack>
</template>
