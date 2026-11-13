<template>
  <UiCard data-testid="service-ssh">
    <UiCardHeader :title="$t('client.services.ssh.title')" :icon="Terminal" />

    <UiStack v-if="loading && !access" data-testid="service-ssh-loading">
      <UiSkeleton shape="block" class="h-14" />
      <UiSkeleton shape="block" class="h-24" />
    </UiStack>

    <UiStack v-else-if="loadError" gap="sm">
      <UiAlert variant="error" data-testid="service-ssh-load-error">{{ loadError }}</UiAlert>
      <UiButton variant="outline" size="sm" data-testid="service-ssh-retry" @click="load">
        {{ $t('client.services.ssh.retry') }}
      </UiButton>
    </UiStack>

    <UiStack v-else-if="access" gap="lg">
      <UiAlert v-if="!canManage" :variant="stateVariant" data-testid="service-ssh-state">
        {{ $t(`client.services.ssh.state.${access.state}`) }}
      </UiAlert>

      <UiGrid v-if="canManage && access.hostname" cols="3" gap="sm" data-testid="service-ssh-connection">
        <UiDescriptionItem :label="$t('client.services.ssh.hostname')" :value="access.hostname" value-class="font-mono" />
        <UiDescriptionItem :label="$t('client.services.ssh.port')" :value="access.port" value-class="font-mono" />
        <UiDescriptionItem :label="$t('client.services.ssh.username')" :value="access.username" value-class="font-mono" />
      </UiGrid>

      <UiStack v-if="access.connectCommand" gap="xs">
        <UiText size="xs" tone="muted">{{ $t('client.services.ssh.connectCommand') }}</UiText>
        <UiStack direction="row" gap="sm" align="stretch">
          <code
            class="flex-1 min-w-0 overflow-x-auto whitespace-nowrap rounded-lg bg-gray-900 text-gray-100 px-3 py-2 text-xs font-mono"
            data-testid="service-ssh-command"
          >{{ access.connectCommand }}</code>
          <UiButton variant="secondary" size="sm" data-testid="service-ssh-copy" @click="copy(access.connectCommand)">
            <component :is="copied ? Check : Copy" :size="14" :stroke-width="2" class="mr-1.5" />
            {{ copied ? $t('client.services.ssh.copied') : $t('client.services.ssh.copy') }}
          </UiButton>
        </UiStack>
      </UiStack>

      <UiStack v-if="canManage || keys.length > 0" gap="sm" data-testid="service-ssh-keys">
        <UiText weight="semibold">
          {{ $t('client.services.ssh.keysTitle', { count: keys.length, max: access.maxKeys }) }}
        </UiText>
        <UiAlert v-if="revokeError" variant="error" data-testid="service-ssh-revoke-error">{{ revokeError }}</UiAlert>
        <UiText v-if="keys.length === 0" size="sm" tone="muted" data-testid="service-ssh-empty">
          {{ $t('client.services.ssh.noKeys') }}
        </UiText>
        <UiStack v-for="key in keys" :key="key.keyId" direction="row" gap="sm" align="center" wrap data-testid="service-ssh-key">
          <UiStack gap="none" class="flex-1 min-w-0">
            <UiText weight="medium" truncate data-testid="service-ssh-key-label">{{ key.label }}</UiText>
            <UiText size="xs" tone="muted" class="font-mono break-all" data-testid="service-ssh-key-fingerprint">{{ key.fingerprint }}</UiText>
            <UiText size="xs" tone="subtle">{{ $t('client.services.ssh.addedOn', { date: formatDate(key.createdAt) }) }}</UiText>
          </UiStack>
          <UiBadge v-if="badgeKey(key)" variant="warning" data-testid="service-ssh-key-badge">{{ $t(badgeKey(key)) }}</UiBadge>
          <UiButton
            v-if="canRevoke"
            variant="danger"
            size="sm"
            :loading="revokingId === key.keyId"
            data-testid="service-ssh-revoke"
            @click="pendingRevoke = key"
          >
            <Trash2 :size="14" :stroke-width="2" class="mr-1.5" />
            {{ $t('client.services.ssh.revoke') }}
          </UiButton>
        </UiStack>
      </UiStack>

      <UiStack v-if="canManage" gap="sm">
        <UiText weight="semibold">{{ $t('client.services.ssh.addTitle') }}</UiText>
        <UiAlert v-if="atLimit" variant="info" data-testid="service-ssh-limit">{{ $t('client.services.ssh.limitReached') }}</UiAlert>
        <UiForm v-else data-testid="service-ssh-form" :error="formError" spacing="md" @submit="submit">
          <UiTextarea
            v-model="publicKey"
            :label="$t('client.services.ssh.publicKey')"
            :placeholder="$t('client.services.ssh.publicKeyPlaceholder')"
            :hint="$t('client.services.ssh.publicKeyHint')"
            :rows="3"
            :max-length="1024"
            testid="service-ssh-public-key"
            required
            @update:model-value="onKeyInput"
          />
          <UiInput
            v-model="label"
            :label="$t('client.services.ssh.label')"
            :placeholder="$t('client.services.ssh.labelPlaceholder')"
            testid="service-ssh-label"
            required
          />
          <template #actions>
            <UiButton type="submit" :loading="adding" data-testid="service-ssh-add">
              <Plus v-if="!adding" :size="14" :stroke-width="2" class="mr-1.5" />
              {{ $t('client.services.ssh.add') }}
            </UiButton>
          </template>
        </UiForm>
      </UiStack>

      <UiAccordion v-if="canManage" :items="howTo" data-testid="service-ssh-howto">
        <template #content-0>
          <UiStack gap="sm">
            <UiText size="sm">{{ $t('client.services.ssh.howToGenerate') }}</UiText>
            <code class="block overflow-x-auto rounded bg-gray-900 text-gray-100 px-3 py-2 text-xs font-mono">{{ GENERATE_COMMAND }}</code>
            <UiText size="sm">{{ $t('client.services.ssh.howToCopy') }}</UiText>
            <code class="block overflow-x-auto rounded bg-gray-900 text-gray-100 px-3 py-2 text-xs font-mono">{{ SHOW_PUBLIC_KEY_COMMAND }}</code>
            <UiText size="sm">{{ $t('client.services.ssh.howToNeverPrivate') }}</UiText>
          </UiStack>
        </template>
        <template #content-1>
          <UiStack gap="md" data-testid="service-ssh-deploy">
            <UiText size="sm">{{ $t('client.services.ssh.deploy.intro') }}</UiText>
            <UiStack v-for="step in deploySteps" :key="step.key" gap="xs">
              <UiText size="sm" weight="semibold">{{ $t(`client.services.ssh.deploy.${step.key}`, { secret: DEPLOY_SECRET_NAME }) }}</UiText>
              <UiText v-if="step.hint" size="xs" tone="muted">{{ $t(`client.services.ssh.deploy.${step.hint}`, { secret: DEPLOY_SECRET_NAME }) }}</UiText>
              <UiStack v-if="step.code" direction="row" gap="sm" align="start">
                <pre
                  class="flex-1 min-w-0 overflow-x-auto rounded bg-gray-900 text-gray-100 px-3 py-2 text-xs font-mono"
                  :data-testid="`service-ssh-deploy-${step.key}`"
                >{{ step.code }}</pre>
                <UiButton variant="secondary" size="sm" :data-testid="`service-ssh-deploy-copy-${step.key}`" @click="copySnippet(step.key, step.code)">
                  <component :is="copiedSnippet === step.key ? Check : Copy" :size="14" :stroke-width="2" class="mr-1.5" />
                  {{ copiedSnippet === step.key ? $t('client.services.ssh.copied') : $t('client.services.ssh.copy') }}
                </UiButton>
              </UiStack>
            </UiStack>
          </UiStack>
        </template>
      </UiAccordion>
    </UiStack>

    <UiConfirmDialog
      :open="Boolean(pendingRevoke)"
      :title="$t('client.services.ssh.revokeConfirmTitle')"
      :description="$t('client.services.ssh.revokeConfirmDesc', { label: pendingRevoke?.label ?? '', fingerprint: pendingRevoke?.fingerprint ?? '' })"
      :confirm-label="$t('client.services.ssh.revoke')"
      variant="danger"
      :loading="Boolean(revokingId)"
      @confirm="confirmRevoke"
      @cancel="pendingRevoke = null"
    />
  </UiCard>
</template>

<script setup lang="ts">
/**
 * ServiceSshKeys. The SSH Keys panel of a hosting service: access status, connection details,
 * the key list with revoke, the add form and key-generation instructions. Only the service id is
 * ever sent; the server, account, hostname and port all come back from the backend. Requests,
 * flags and refusals belong to the SSH key store.
 */
import { computed, onMounted, ref } from 'vue'
import { Check, Copy, Plus, Terminal, Trash2 } from 'lucide-vue-next'
import { storeToRefs } from 'pinia'
import { useSshKeysStore } from '~/stores/sshkeys'
import { ActionOutcome } from '~/types/actionoutcome'
import { SshAccessState } from '~/types/sshaccessstate'
import type { SshKey } from '~/types/sshkey'
import { SshKeySyncState } from '~/types/sshkeysyncstate'
import { formatDate } from '~/utils/formatDate'
import { suggestedLabel } from '~/utils/sshKey'
import { DEPLOY_KEYGEN_COMMAND, DEPLOY_SECRET_NAME, githubDeployWorkflow, gitlabDeployJob } from '~/utils/sshDeploy'
import { UiAlertVariant } from '~/utils/ui/enums'

/** The command that generates an ED25519 key pair. */
const GENERATE_COMMAND = 'ssh-keygen -t ed25519 -C "you@example.com"'

/** The command that prints the public half. */
const SHOW_PUBLIC_KEY_COMMAND = 'cat ~/.ssh/id_ed25519.pub'

/** How long the "copied" state stays on the copy button, in milliseconds. */
const COPIED_FLASH_MS = 2000

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of ServiceSshKeys. */
interface Props {
  /** The id of the service whose keys are shown. */
  serviceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The SSH key store. */
const store = useSshKeysStore()
/** Reactive state of the store this component renders. */
const { access, keys, loading, loadError, adding, addError, localError, revokingId, revokeError, canManage, canRevoke, atLimit } = storeToRefs(store)

/** The public key being pasted. */
const publicKey = ref('')

/** The label being typed. */
const label = ref('')

/** Whether the connect command was just copied. */
const copied = ref(false)

/** The key awaiting revoke confirmation. */
const pendingRevoke = ref<SshKey | null>(null)

/** Alert colour for a state other than available: a fault is an error, a policy is information. */
const stateVariant = computed(() => {
  if (access.value?.state === SshAccessState.Unreachable) {
    return UiAlertVariant.Error
  }
  return access.value?.state === SshAccessState.ShellDisabled ? UiAlertVariant.Warning : UiAlertVariant.Info
})

/** The form's error line: a browser-side refusal first, else the API's sentence. */
const formError = computed(() => (localError.value ? t(`client.services.ssh.localError.${localError.value}`) : addError.value))

/** One step of the deployment guide: its text, an optional hint and an optional snippet to copy. */
interface DeployStep {
  /** Locale key under client.services.ssh.deploy, also the snippet's test id. */
  key: string
  /** Locale key of a muted hint line, if any. */
  hint?: string
  /** Text to show in a code block with a copy button, if any. */
  code?: string
}

/** The deployment guide, filled with this account's details; empty until they are known. */
const deploySteps = computed<DeployStep[]>(() => {
  const a = access.value
  if (!a?.hostname || !a.port || !a.username) {
    return []
  }
  const target = { hostname: a.hostname, port: a.port, username: a.username }
  return [
    { key: 'step1', hint: 'step1Hint', code: DEPLOY_KEYGEN_COMMAND },
    { key: 'step2', hint: 'step2Hint' },
    { key: 'step3', hint: 'step3Hint' },
    { key: 'step4GitHub', hint: 'step4GitHubHint', code: githubDeployWorkflow(target) },
    { key: 'step4GitLab', hint: 'step4GitLabHint', code: gitlabDeployJob(target) },
    { key: 'step5' }
  ]
})

/** The instruction panels: creating a key, then deploying from CI once the account is known. */
const howTo = computed(() => [
  { title: t('client.services.ssh.howToTitle') },
  ...(deploySteps.value.length ? [{ title: t('client.services.ssh.deploy.title') }] : [])
])

/** Which deployment snippet was just copied, if any. */
const copiedSnippet = ref('')

/**
 * The badge a key carries, if any: stale first, then an unconfirmed state, then missing on the server.
 *
 * @param key - The key.
 * @returns The locale key of the badge text, or an empty string for none.
 */
const badgeKey = (key: SshKey): string => {
  if (key.isStale) {
    return 'client.services.ssh.sync.Stale'
  }
  if (key.syncState !== SshKeySyncState.Installed) {
    return `client.services.ssh.sync.${key.syncState}`
  }
  return key.presentOnServer === false ? 'client.services.ssh.sync.Missing' : ''
}

/**
 * Loads the panel for this service.
 *
 * @returns Resolves when loaded; failures are rendered from the store.
 */
const load = (): Promise<void> => store.load(props.serviceId)

/**
 * Pre-fills the label from the key comment while the label is still empty.
 *
 * @param value - The pasted key text.
 */
const onKeyInput = (value: string): void => {
  if (!label.value) {
    label.value = suggestedLabel(value)
  }
}

/**
 * Submits the add form and clears it on success.
 *
 * @returns Resolves when the attempt has ended.
 */
const submit = async (): Promise<void> => {
  if (await store.add(props.serviceId, label.value, publicKey.value) === ActionOutcome.Succeeded) {
    publicKey.value = ''
    label.value = ''
  }
}

/**
 * Revokes the key awaiting confirmation.
 *
 * @returns Resolves when the attempt has ended.
 */
const confirmRevoke = async (): Promise<void> => {
  const key = pendingRevoke.value
  if (!key) {
    return
  }
  await store.revoke(props.serviceId, key.keyId)
  pendingRevoke.value = null
}

/**
 * Copies text to the clipboard and flags it briefly. A blocked clipboard leaves the command
 * selectable, so the failure needs no message.
 *
 * @param text - The text to copy.
 * @returns Resolves when the attempt has ended.
 */
const copy = async (text: string): Promise<void> => {
  await navigator.clipboard?.writeText(text).then(() => {
    copied.value = true
    setTimeout(() => {
      copied.value = false
    }, COPIED_FLASH_MS)
  }, () => undefined)
}

/**
 * Copies a deployment snippet and flags its button briefly. A blocked clipboard leaves the
 * snippet selectable, so the failure needs no message.
 *
 * @param key - The step the snippet belongs to.
 * @param text - The snippet.
 * @returns Resolves when the attempt has ended.
 */
const copySnippet = async (key: string, text: string): Promise<void> => {
  await navigator.clipboard?.writeText(text).then(() => {
    copiedSnippet.value = key
    setTimeout(() => {
      copiedSnippet.value = ''
    }, COPIED_FLASH_MS)
  }, () => undefined)
}

onMounted(() => {
  store.reset()
  return load()
})
</script>
