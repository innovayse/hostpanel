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
      <div v-if="open" class="launcher-panel-wrap">
        <div class="launcher-panel-inner">
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

interface AppEntry {
  id: string
  name: string
  desc: string
  icon: string
  url: string
  comingSoon: boolean
}

const ICON_MAP: Record<string, string> = {
  account:  'lucide:user-circle',
  tasks:    'lucide:list-checks',
  hostpanel:'lucide:server',
  erp:      'lucide:building-2',
  sheets:   'lucide:table',
  email:    'lucide:mail',
  docs:     'lucide:file-text',
  calendar: 'lucide:calendar',
  drive:    'lucide:hard-drive',
}

const apps = ref<AppEntry[]>([])

async function fetchApps() {
  try {
    const data: AppEntry[] = await fetch(`${config.public.mainUrl}/api/portal/public/apps`).then(r => r.json())
    apps.value = data.map((app: AppEntry) => ({
      ...app,
      icon: ICON_MAP[app.id] ?? 'lucide:box',
    }))
  } catch {
    // API недоступен — оставляем пустой список
  }
}

function toggle() {
  open.value = !open.value
  if (open.value && apps.value.length === 0) fetchApps()
}

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
.launcher-panel-wrap {
  position: absolute;
  right: 0;
  top: 100%;
  padding-top: 8px;
  z-index: 50;
}

.launcher-panel-inner {
  width: min(360px, calc(100vw - 16px));
  border-radius: 12px;
  overflow: hidden;
  background: rgba(17, 24, 39, 0.97);
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(12px);
}


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
