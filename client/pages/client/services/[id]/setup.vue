<template>
  <div class="min-h-[60vh] flex items-center justify-center py-10">
    <div class="max-w-xl w-full">
      <!-- Loading State -->
      <div v-if="pending" class="text-center p-12">
        <div class="w-16 h-16 border-4 border-cyan-500/20 border-t-cyan-500 rounded-full animate-spin mx-auto mb-6" />
        <h2 class="text-xl font-bold text-white mb-2">{{ $t('client.setup.loading') || 'Preparing Setup Wizard...' }}</h2>
        <p class="text-gray-500">{{ $t('client.setup.pleaseWait') || 'Fetching your service details.' }}</p>
      </div>

      <!-- Error State -->
      <div v-else-if="error || !service" class="bg-white/5 border border-white/10 rounded-3xl p-10 text-center">
        <div class="w-20 h-20 rounded-full bg-red-500/10 border border-red-500/20 flex items-center justify-center mx-auto mb-6 text-red-400">
          <AlertCircle :size="32" />
        </div>
        <h2 class="text-2xl font-bold text-white mb-4">{{ $t('client.setup.errorTitle') || 'Setup Not Available' }}</h2>
        <p class="text-gray-400 mb-8">{{ $t('client.setup.errorDesc') || 'This service cannot be configured at this time. It might already be active or requires manual intervention.' }}</p>
        <UiButton to="/client/services" variant="subtle">{{ $t('client.setup.backToServices') || 'Back to Services' }}</UiButton>
      </div>

      <!-- Active Service (Already configured) -->
      <div v-else-if="service.status === 'Active' && service.username && service.domain" class="bg-white/5 border border-white/10 rounded-3xl p-10 text-center">
        <div class="w-20 h-20 rounded-full bg-green-500/10 border border-green-500/20 flex items-center justify-center mx-auto mb-6 text-green-400">
          <CheckCircle :size="32" />
        </div>
        <h2 class="text-2xl font-bold text-white mb-4">{{ $t('client.setup.alreadyActive') || 'Already Configured' }}</h2>
        <p class="text-gray-400 mb-8">{{ $t('client.setup.alreadyActiveDesc') || 'This hosting service is already set up and active.' }}</p>
        <UiButton :to="`/client/services/${service.id}`" variant="primary">{{ $t('client.setup.goToManage') || 'Manage Service' }}</UiButton>
      </div>

      <!-- Setup Wizard -->
      <div v-else class="space-y-8">
        <!-- Progress Header -->
        <div class="text-center mb-10">
          <div class="inline-flex items-center gap-2 px-3 py-1 mb-4 rounded-full glass border border-white/10 text-xs font-semibold tracking-wider uppercase text-cyan-400">
            Step {{ currentStep }} of {{ totalSteps }}
          </div>
          <h1 class="text-3xl font-bold text-white mb-2">{{ stepTitle }}</h1>
          <p class="text-gray-400">{{ stepSubtitle }}</p>
        </div>

        <!-- Progress Bar -->
        <div class="h-1.5 w-full bg-white/5 rounded-full overflow-hidden mb-12">
          <div 
            class="h-full bg-gradient-to-r from-cyan-500 to-primary-500 transition-all duration-500"
            :style="{ width: `${(currentStep / totalSteps) * 100}%` }" />
        </div>

        <!-- Step 1: Choose Domain -->
        <div v-if="currentStep === 1" 
             v-motion
             :initial="{ opacity: 0, x: 20 }"
             :enter="{ opacity: 1, x: 0 }"
             class="bg-white/5 border border-white/10 rounded-3xl p-8 backdrop-blur-xl">
          <div class="flex items-center gap-4 mb-8">
            <div class="w-12 h-12 rounded-2xl bg-cyan-500/20 border border-cyan-500/30 flex items-center justify-center text-cyan-400">
              <Globe :size="24" />
            </div>
            <div>
              <h3 class="text-lg font-bold text-white">Your Domain</h3>
              <p class="text-sm text-gray-500">Every hosting needs a domain to start</p>
            </div>
          </div>

          <div class="space-y-6">
            <UiInput v-model="setupData.domain" label="Domain Name" placeholder="example.com" required />
            <p class="text-xs text-gray-500">
              If you haven't bought a domain yet, enter the domain you plan to use. You'll need to point its nameservers to us later.
            </p>
            <UiButton full-width size="lg" :disabled="!setupData.domain" @click="nextStep">
              Continue
              <ArrowRight :size="18" class="ml-2" />
            </UiButton>
          </div>
        </div>

        <!-- Step 2: Credentials -->
        <div v-if="currentStep === 2" 
             v-motion
             :initial="{ opacity: 0, x: 20 }"
             :enter="{ opacity: 1, x: 0 }"
             class="bg-white/5 border border-white/10 rounded-3xl p-8 backdrop-blur-xl">
          <div class="flex items-center gap-4 mb-8">
            <div class="w-12 h-12 rounded-2xl bg-primary-500/20 border border-primary-500/30 flex items-center justify-center text-primary-400">
              <Lock :size="24" />
            </div>
            <div>
              <h3 class="text-lg font-bold text-white">Access Credentials</h3>
              <p class="text-sm text-gray-500">Secure your hosting control panel</p>
            </div>
          </div>

          <div class="space-y-6">
            <div>
              <UiInput
                v-model="setupData.username"
                label="Control Panel Username"
                placeholder="e.g. webmaster"
                required
                @input="sanitizeUsername"
              />
              <p class="text-xs text-gray-500 mt-1.5">Lowercase letters and numbers only, no spaces or special characters.</p>
              <p v-if="usernameError" class="text-xs text-red-400 mt-1">{{ usernameError }}</p>
            </div>
            <UiInput v-model="setupData.password" type="password" label="Control Panel Password" placeholder="••••••••" required />

            <div class="flex gap-4 pt-2">
              <UiButton variant="subtle" full-width @click="prevStep">Back</UiButton>
              <UiButton variant="primary" full-width :disabled="!setupData.username || !setupData.password || !!usernameError" @click="nextStep">
                Confirm Details
              </UiButton>
            </div>
          </div>
        </div>

        <!-- Step 3: Confirmation -->
        <div v-if="currentStep === 3" 
             v-motion
             :initial="{ opacity: 0, scale: 0.95 }"
             :enter="{ opacity: 1, scale: 1 }"
             class="bg-white/5 border border-white/10 rounded-3xl p-8 backdrop-blur-xl">
          <div class="text-center mb-8">
            <div class="w-20 h-20 rounded-full bg-cyan-500/10 border border-cyan-500/20 flex items-center justify-center mx-auto mb-6 text-cyan-400">
              <Rocket :size="32" />
            </div>
            <h3 class="text-xl font-bold text-white">Ready to Launch?</h3>
            <p class="text-gray-400 mt-2">We're about to configure your server with the following details:</p>
          </div>

          <div class="space-y-3 mb-8 p-4 rounded-2xl bg-white/5 border border-white/5">
            <div class="flex justify-between text-sm">
              <span class="text-gray-500">Plan:</span>
              <span class="text-white font-medium">{{ service.name }}</span>
            </div>
            <div class="flex justify-between text-sm">
              <span class="text-gray-500">Domain:</span>
              <span class="text-white font-medium">{{ setupData.domain }}</span>
            </div>
            <div class="flex justify-between text-sm">
              <span class="text-gray-500">Username:</span>
              <span class="text-white font-medium italic">{{ setupData.username }}</span>
            </div>
          </div>

          <!-- Error Message if any -->
          <div v-if="setupError" class="mb-6 p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm flex gap-3">
            <AlertCircle :size="18" class="shrink-0" />
            <p>{{ setupError }}</p>
          </div>

          <div class="space-y-3">
            <UiButton variant="primary" full-width size="lg" :loading="submitting" @click="finishSetup">
              <Zap v-if="!submitting" :size="18" class="mr-2" />
              Build My Server
            </UiButton>
            <UiButton variant="ghost" full-width :disabled="submitting" @click="prevStep">I need to change something</UiButton>
          </div>

          <p class="text-[10px] text-center text-gray-500 mt-6 uppercase tracking-widest font-bold">
            Account provisioning takes about 60 seconds
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Globe, Lock, Rocket, ArrowRight, Zap, CheckCircle, AlertCircle } from 'lucide-vue-next'
import { useClientStore } from '~/stores/client'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()
const route = useRoute()
const serviceId = route.params.id as string

/** Simplified interface for the service for template usage */
interface ClientService {
  id: number
  name: string
  status: string
  domain: string
  username: string
}

const { data: service, pending, error } = await useApi<ClientService>(`/api/portal/client/services/${serviceId}`)

const currentStep = ref(1)
const totalSteps = 3
const setupError = ref('')
const submitting = ref(false)

const setupData = reactive({
  domain: '',
  username: '',
  password: ''
})

const usernameError = computed(() => {
  const u = setupData.username
  if (!u) return ''
  if (!/^[a-z][a-z0-9]*$/.test(u)) return 'Must start with a letter and contain only lowercase letters and numbers.'
  if (u.length < 3) return 'Must be at least 3 characters.'
  if (u.length > 8) return 'Must be 8 characters or less (server requirement).'
  return ''
})

function sanitizeUsername() {
  setupData.username = setupData.username.toLowerCase().replace(/[^a-z0-9]/g, '')
}

const stepTitle = computed(() => {
  if (currentStep.value === 1) return 'Pick your domain'
  if (currentStep.value === 2) return 'Set your credentials'
  return 'Final Review'
})

const stepSubtitle = computed(() => {
  if (currentStep.value === 1) return 'Choose the address for your new website'
  if (currentStep.value === 2) return 'These details will be used to log in to your control panel'
  return 'Double check everything before we create your account'
})

function nextStep() {
  if (currentStep.value < totalSteps) currentStep.value++
}

function prevStep() {
  if (currentStep.value > 1) currentStep.value--
}

async function finishSetup() {
  submitting.value = true
  setupError.value = ''
  
  try {
    // This API endpoint will need to be created or updated to handle service provisioning
    await apiFetch(`/api/portal/client/services/${serviceId}/setup`, {
      method: 'POST',
      body: setupData
    })
    
    // Success - redirect to the service management page
    await navigateTo(`/client/services/${serviceId}`)
  } catch (err: any) {
    setupError.value = err?.data?.statusMessage || 'An error occurred during setup. Please try again or contact support.'
  } finally {
    submitting.value = false
  }
}

useHead({ title: `Service Setup — Innovayse` })
</script>
