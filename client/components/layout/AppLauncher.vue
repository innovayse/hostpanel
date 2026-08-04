<template>
  <div ref="rootEl" class="relative">
    <button
      type="button"
      class="flex items-center justify-center w-9 h-9 rounded-lg text-gray-300 hover:text-white border border-white/10 hover:border-white/20 transition-all duration-200"
      aria-label="Innovayse apps"
      @click="toggle"
    >
      <Icon name="lucide:layout-grid" size="16" />
    </button>

    <Transition name="launcher-panel">
      <div v-if="open" class="absolute right-0 top-full pt-2 z-50">
        <div class="w-80 rounded-xl overflow-hidden" style="background:rgba(17,24,39,0.97);border:1px solid rgba(255,255,255,0.1);box-shadow:0 25px 50px -12px rgba(0,0,0,0.5);backdrop-filter:blur(12px);">
          <div class="grid grid-cols-3 gap-2 p-3">
            <template v-for="app in apps" :key="app.id">
              <!-- Coming soon -->
              <div
                v-if="app.comingSoon"
                class="flex flex-col items-center text-center gap-2 rounded-lg p-3 opacity-40 cursor-not-allowed select-none"
              >
                <span class="flex items-center justify-center w-10 h-10 rounded-lg" style="background:rgba(14,165,233,0.2);color:#38bdf8;">
                  <Icon :name="app.icon" :size="20" />
                </span>
                <span class="text-xs font-medium text-white">{{ app.name }}</span>
                <span class="text-[10px] text-gray-500 leading-snug">{{ app.desc }}</span>
                <span class="text-[10px] font-medium text-gray-500">Coming soon</span>
              </div>
              <!-- Active app -->
              <a
                v-else
                :href="app.url"
                target="_blank"
                rel="noopener noreferrer"
                class="flex flex-col items-center text-center gap-2 rounded-lg p-3 transition-colors hover:bg-white/8 cursor-pointer"
                style="text-decoration:none;"
                @click="open = false"
              >
                <span class="flex items-center justify-center w-10 h-10 rounded-lg" style="background:rgba(14,165,233,0.2);color:#38bdf8;">
                  <Icon :name="app.icon" :size="20" />
                </span>
                <span class="text-xs font-medium text-white">{{ app.name }}</span>
                <span class="text-[10px] text-gray-500 leading-snug">{{ app.desc }}</span>
              </a>
            </template>
          </div>
          <!-- Footer -->
          <div class="border-t border-white/10">
            <a
              :href="config.public.baseUrl + '/account'"
              target="_blank"
              rel="noopener noreferrer"
              class="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-300 hover:text-white hover:bg-white/8 transition-colors"
              style="text-decoration:none;"
              @click="open = false"
            >
              <Icon name="lucide:layout-dashboard" size="15" class="text-sky-400 flex-shrink-0" />
              Manage all services
            </a>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup lang="ts">
defineProps<{ isLoggedIn: boolean }>()

const config = useRuntimeConfig()
const open = ref(false)
const rootEl = ref<HTMLElement | null>(null)

const isInnovayse = computed(() => config.public.authMode === 'sso')

const apps = computed(() => {
  const base = [
    { id: 'account', name: 'Account', desc: 'Your profile & settings', icon: 'lucide:user-circle', url: config.public.baseUrl + '/account', comingSoon: false },
  ]
  if (!isInnovayse.value) return base
  return [
    ...base,
    { id: 'tasks',     name: 'Tasks',     desc: 'Project & task management',  icon: 'lucide:list-checks', url: config.public.tasksUrl + '/auth/gitlab/', comingSoon: false },
    { id: 'erp',       name: 'ERP',       desc: 'Business & operations',       icon: 'lucide:building-2',  url: config.public.erpUrl + '/app',            comingSoon: false },
    { id: 'sheets',    name: 'Sheets',    desc: 'Collaborative spreadsheets',  icon: 'lucide:table',       url: config.public.sheetsUrl,                  comingSoon: false },
    { id: 'email',     name: 'Email',     desc: 'Read and send emails',        icon: 'lucide:mail',        url: config.public.emailUrl,                   comingSoon: false },
    { id: 'calendar',  name: 'Calendar',  desc: 'Team calendar & scheduling',  icon: 'lucide:calendar',    url: config.public.calendarUrl,                comingSoon: false },
    { id: 'nextcloud', name: 'Files',     desc: 'Files & collaboration',       icon: 'lucide:folder',      url: config.public.nextcloudUrl,               comingSoon: true  },
  ]
})

function toggle() { open.value = !open.value }

function onClickOutside(e: MouseEvent) {
  if (open.value && rootEl.value && !rootEl.value.contains(e.target as Node)) {
    open.value = false
  }
}

function onEscape(e: KeyboardEvent) {
  if (e.key === 'Escape') open.value = false
}

onMounted(() => {
  document.addEventListener('click', onClickOutside)
  document.addEventListener('keydown', onEscape)
})

onUnmounted(() => {
  document.removeEventListener('click', onClickOutside)
  document.removeEventListener('keydown', onEscape)
})
</script>

<style scoped>
.launcher-panel-enter-active,
.launcher-panel-leave-active {
  transition: opacity 0.15s ease, transform 0.15s ease;
}
.launcher-panel-enter-from,
.launcher-panel-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}
</style>
