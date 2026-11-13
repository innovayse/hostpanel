<template>
  <UiStack gap="lg" data-testid="setup-wizard">
    <UiStack align="center" gap="xs" class="text-center">
      <UiBadge variant="info" rounded data-testid="setup-step-of">{{ stepOf }}</UiBadge>
      <UiHeading :level="1" size="section">{{ stepTitle }}</UiHeading>
      <UiText tone="muted">{{ stepSubtitle }}</UiText>
    </UiStack>

    <UiProgress :value="(currentStep / TOTAL_STEPS) * 100" :label="stepOf" />

    <ClientServicesSetupDomainStep
      v-if="currentStep === SetupStep.Domain"
      v-model:domain="setupData.domain"
      :domain-error="domainError"
      :has-purchased-domain="hasPurchasedDomain"
      class="animate-reveal-up motion-reduce:animate-none"
      @next="nextStep"
    />

    <ClientServicesSetupCredentialsStep
      v-else-if="currentStep === SetupStep.Credentials"
      v-model:username="setupData.username"
      v-model:password="setupData.password"
      :username-error="usernameError"
      :password-error="passwordError"
      :can-continue="canContinue"
      class="animate-reveal-up motion-reduce:animate-none"
      @back="prevStep"
      @next="nextStep"
    />

    <ClientServicesSetupReviewStep
      v-else
      :plan-name="service.productName"
      :domain="setupData.domain"
      :username="setupData.username"
      :submitting="setupSubmitting"
      :error="setupError"
      class="animate-reveal-up motion-reduce:animate-none"
      @build="finishSetup"
      @back="prevStep"
    />
  </UiStack>
</template>

<script setup lang="ts">
/**
 * SetupWizard. The three-step first-time setup of a hosting service: domain, control-panel
 * credentials, review. The typed values are checked here as the client types (the checks mirror
 * the server's validator); provisioning, its in-flight flag and the API's refusal belong to the
 * service management store. A successful setup opens the service's management page.
 */
import { clientServicePath } from '~/constants/clientRoutes'
import { computed, reactive, ref } from 'vue'
import { storeToRefs } from 'pinia'
import { useServiceManagementStore } from '~/stores/servicemanagement'
import { ActionOutcome } from '~/types/actionoutcome'
import type { ClientService } from '~/types/clientservice'
import { SetupStep } from '~/types/setupstep'
import { domainErrorKey, passwordErrorKey, usernameErrorKey } from '~/utils/hostingSetup'

/** How many steps the wizard has. */
const TOTAL_STEPS = SetupStep.Review

/** Translates a key into the active locale. */
const { t } = useI18n()

/** Props of SetupWizard. */
interface Props {
  /** The service being set up. */
  service: ClientService
  /** The id of the service, for provisioning and for the page opened afterwards. */
  serviceId: string
}

/** The props of the component. */
const props = defineProps<Props>()

/** The service management store: provisioning, its in-flight flag and the API's refusal. */
const mgmt = useServiceManagementStore()
/** Reactive state of the store this component renders. */
const { setupError, setupSubmitting } = storeToRefs(mgmt)

/** The step shown. */
const currentStep = ref<SetupStep>(SetupStep.Domain)

/** What the client has typed so far; the domain starts as the one bought with the plan. */
const setupData = reactive({
  domain: props.service.domain || '',
  username: '',
  password: '',
})

/** Whether the domain was purchased alongside this hosting service. */
const hasPurchasedDomain = computed(() => Boolean(props.service.domain))

/** "Step 2 of 3", the badge above the title and the name of the progress bar. */
const stepOf = computed(() => t('client.setup.stepOf', { current: currentStep.value, total: TOTAL_STEPS }))

/**
 * Translates the i18n key a check named.
 *
 * @param key - The key, or an empty string when the value is acceptable.
 * @returns The sentence, or an empty string.
 */
const sentenceFor = (key: string): string => (key ? t(key) : '')

/** Why the typed domain is not a valid hostname; empty while it is blank or valid. */
const domainError = computed(() => sentenceFor(domainErrorKey(setupData.domain)))

/** Why the typed username is not acceptable; empty while it is blank or valid. */
const usernameError = computed(() => sentenceFor(usernameErrorKey(setupData.username)))

/** Why the typed password is too weak; empty while it is blank or long enough. */
const passwordError = computed(() => sentenceFor(passwordErrorKey(setupData.password)))

/** Whether the credentials step may continue: both values filled in and acceptable. */
const canContinue = computed(() => Boolean(setupData.username) && Boolean(setupData.password) && !usernameError.value && !passwordError.value)

/** Title of the current step. */
const stepTitle = computed(() => {
  if (currentStep.value === SetupStep.Domain) {
    return t('client.setup.stepDomainTitle')
  }
  return currentStep.value === SetupStep.Credentials ? t('client.setup.stepCredentialsTitle') : t('client.setup.stepReviewTitle')
})

/** Subtitle of the current step. */
const stepSubtitle = computed(() => {
  if (currentStep.value === SetupStep.Domain) {
    return t('client.setup.stepDomainSubtitle')
  }
  return currentStep.value === SetupStep.Credentials ? t('client.setup.stepCredentialsSubtitle') : t('client.setup.stepReviewSubtitle')
})

/** Moves to the next step, if there is one. */
const nextStep = (): void => {
  if (currentStep.value < TOTAL_STEPS) {
    currentStep.value += 1
  }
}

/** Moves to the previous step, if there is one. */
const prevStep = (): void => {
  if (currentStep.value > SetupStep.Domain) {
    currentStep.value -= 1
  }
}

/**
 * Submits first-time setup and, when the API accepts it, opens the service management page.
 *
 * @returns Resolves when the attempt has ended; a refusal is rendered from `setupError`.
 */
const finishSetup = async (): Promise<void> => {
  const outcome = await mgmt.setUpService(props.serviceId, setupData)
  if (outcome !== ActionOutcome.Succeeded) {
    return
  }

  // Clear the cached data of this service so the detail page refetches it.
  clearNuxtData()

  await navigateTo(clientServicePath(props.serviceId))
}
</script>
