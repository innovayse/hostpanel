<template>
  <div class="max-w-2xl">
    <NuxtLink to="/client/tickets" class="inline-flex items-center gap-2 text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white text-sm transition-colors mb-8">
      <ArrowLeft :size="16" :stroke-width="2" />
      {{ $t('client.tickets.backTo') }}
    </NuxtLink>

    <div class="mb-8">
      <h1 class="text-2xl font-bold text-gray-900 dark:text-white">{{ $t('client.tickets.newTitle') }}</h1>
      <p class="text-gray-500 dark:text-gray-400 text-sm mt-1">{{ $t('client.tickets.newSubtitle') }}</p>
    </div>

    <!-- Success state -->
    <div v-if="submitted" class="p-6 rounded-2xl bg-green-500/10 border border-green-500/30 text-center">
      <CheckCircle :size="48" :stroke-width="2" class="text-green-400 mx-auto mb-4" />
      <h2 class="text-gray-900 dark:text-white font-bold text-lg mb-2">{{ $t('client.tickets.successTitle') }}</h2>
      <p class="text-gray-500 dark:text-gray-400 text-sm mb-6">{{ $t('client.tickets.successSubtitle') }}</p>
      <NuxtLink
        to="/client/tickets"
        class="px-6 py-2.5 rounded-xl bg-cyan-500 text-white font-semibold text-sm hover:bg-cyan-400 transition-colors"
      >
        {{ $t('client.tickets.viewMyTickets') }}
      </NuxtLink>
    </div>

    <!-- Form -->
    <UiCard v-else>
      <UiForm :error="error" spacing="lg" @submit="handleSubmit">
        <!-- Department -->
        <UiSelect
          v-model="form.deptid"
          :label="$t('client.tickets.deptLabel')"
          :placeholder="deptPending ? $t('client.tickets.deptLoading') : $t('client.tickets.deptPlaceholder')"
          :options="deptOptions"
          :disabled="deptPending"
          required
        />

        <!-- Subject -->
        <UiInput
          v-model="form.subject"
          :label="$t('client.tickets.subjectLabel')"
          type="text"
          :placeholder="$t('client.tickets.subjectPlaceholder')"
          required
        />

        <!-- Priority -->
        <div>
          <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">{{ $t('client.tickets.priorityLabel') }}</label>
          <div class="flex gap-3">
            <button
              v-for="p in priorities"
              :key="p.value"
              type="button"
              class="flex-1 py-2.5 rounded-xl border text-sm font-medium transition-all duration-200"
              :class="form.priority === p.value
                ? `border-${p.color}-500/60 bg-${p.color}-500/10 text-${p.color}-400`
                : 'border-gray-200 dark:border-white/10 text-gray-500 dark:text-gray-400 hover:border-gray-300 dark:hover:border-white/20'"
              @click="form.priority = p.value"
            >
              {{ p.label }}
            </button>
          </div>
        </div>

        <!-- Message -->
        <UiTextarea
          v-model="form.message"
          :label="$t('client.tickets.messageLabel')"
          :placeholder="$t('client.tickets.messagePlaceholder')"
          :rows="6"
          :resize="false"
          required
        />

        <template #actions>
          <UiButton type="submit" :loading="loading" :full-width="true">
            <Send v-if="!loading" :size="16" :stroke-width="2" class="mr-2" />
            {{ loading ? $t('client.tickets.submitting') : $t('client.tickets.submit') }}
          </UiButton>
        </template>
      </UiForm>
    </UiCard>
  </div>
</template>

<script setup lang="ts">
import { ArrowLeft, CheckCircle, Send } from 'lucide-vue-next'

definePageMeta({ layout: 'client', middleware: 'client-auth' })

const { t } = useI18n()

const { data: departments, pending: deptPending } = useApi<{ id: number; name: string }[]>(
  '/api/portal/public/departments',
  { default: () => [] }
)

/** Map departments to UiSelect option format */
const deptOptions = computed(() =>
  (departments.value ?? []).map(d => ({ value: d.id, label: d.name }))
)

const priorities = computed(() => [
  { value: 'Low',    label: t('client.tickets.priorityLow'),    color: 'green'  },
  { value: 'Medium', label: t('client.tickets.priorityMedium'), color: 'yellow' },
  { value: 'High',   label: t('client.tickets.priorityHigh'),   color: 'red'    }
])

const form = reactive({
  deptid:   '' as string | number,
  subject:  '',
  message:  '',
  priority: 'Medium'
})

const loading   = ref(false)
const error     = ref('')
const submitted = ref(false)

async function handleSubmit() {
  loading.value = true
  error.value = ''
  try {
    await apiFetch('/api/portal/client/tickets', {
      method: 'POST',
      body: form
    })
    submitted.value = true
  } catch (err: any) {
    error.value = err?.data?.statusMessage || t('client.tickets.errorDefault')
  } finally {
    loading.value = false
  }
}
</script>
