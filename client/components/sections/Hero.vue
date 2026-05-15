<template>
  <section class="relative min-h-screen flex items-center justify-center bg-[#0a0a0f] overflow-hidden">
    <!-- Animated background blobs -->
    <div class="absolute inset-0 overflow-hidden pointer-events-none">
      <!-- Large animated gradient blobs -->
      <div class="absolute top-0 left-1/4 w-[500px] h-[500px] bg-primary-500/30 rounded-full blur-[120px] animate-blob" />
      <div class="absolute top-1/3 right-1/4 w-[600px] h-[600px] bg-secondary-500/30 rounded-full blur-[120px] animate-blob animation-delay-2000" />
      <div class="absolute bottom-0 left-1/2 w-[400px] h-[400px] bg-cyan-500/20 rounded-full blur-[120px] animate-blob animation-delay-4000" />

      <!-- Grid pattern overlay -->
      <div class="absolute inset-0 opacity-[0.02]">
        <div class="absolute inset-0" style="background-image: linear-gradient(rgba(255,255,255,0.1) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.1) 1px, transparent 1px); background-size: 50px 50px;" />
      </div>

      <!-- Radial gradient overlay for depth -->
      <div class="absolute inset-0 bg-gradient-radial from-transparent via-transparent to-[#0a0a0f]" />
    </div>

    <div class="container-custom relative z-10 py-20">
      <div class="text-center max-w-5xl mx-auto">
        <!-- Eyebrow badge -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { duration: 500 } }"
          class="inline-flex items-center gap-2 px-4 py-2 mb-8 rounded-full glass border border-primary-500/20 backdrop-blur-sm"
        >
          <span class="relative flex h-2 w-2">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-primary-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-2 w-2 bg-primary-500"></span>
          </span>
          <span class="text-sm font-medium text-gray-300">{{ $t('hero.badge') }}</span>
        </div>

        <!-- Main headline with animated gradient -->
        <h1 class="text-5xl sm:text-6xl md:text-7xl lg:text-8xl font-bold mb-6 leading-[1.1] tracking-tight">
          <span
            v-for="(line, index) in $t('hero.title').split('\n')"
            :key="index"
            v-motion
            :initial="{ opacity: 0, scale: 0.95, filter: 'blur(10px)' }"
            :enter="{
              opacity: 1,
              scale: 1,
              filter: 'blur(0px)',
              transition: {
                delay: 300 + index * 150,
                duration: 800,
                type: 'spring',
                stiffness: 100
              }
            }"
            class="block bg-gradient-to-r from-cyan-400 via-primary-400 to-secondary-400 bg-clip-text text-transparent animate-gradient bg-300 drop-shadow-2xl"
            :style="{ animationDelay: `${index * 0.5}s` }"
          >
            {{ line }}
          </span>
        </h1>

        <!-- Subtitle -->
        <p
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 600, duration: 600 } }"
          class="text-lg md:text-xl lg:text-2xl text-gray-300/80 mb-12 max-w-3xl mx-auto leading-relaxed font-light mt-4"
        >
          {{ $t('hero.subtitle') }}
        </p>

        <!-- CTA Buttons -->
        <div
          v-motion
          :initial="{ opacity: 0, y: 20 }"
          :enter="{ opacity: 1, y: 0, transition: { delay: 800, duration: 600 } }"
          class="flex flex-col sm:flex-row items-center justify-center gap-6 sm:gap-10 mb-16 sm:mb-20"
        >
          <!-- Primary CTA -->
          <UiMagnetic :strength="0.3" :range="100">
            <button
              class="w-full sm:w-auto group relative px-8 py-4 sm:px-12 sm:py-6 rounded-2xl font-bold text-white overflow-hidden transition-all duration-500 hover:shadow-[0_0_40px_rgba(14,165,233,0.5)] active:scale-95 flex items-center justify-center gap-3"
              @click="navigateTo(localePath('/contact'))"
            >
              <!-- Background base -->
              <div class="absolute inset-0 bg-gradient-to-br from-primary-600 to-cyan-700" />
              
              <!-- Hover Overlay -->
              <div class="absolute inset-0 bg-gradient-to-tr from-secondary-600/50 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500" />

              <!-- Flare motion -->
              <div class="absolute -inset-full opacity-0 group-hover:opacity-20 transition-opacity duration-700 bg-[radial-gradient(circle,white_0%,transparent_70%)] group-hover:animate-flare" />
              
              <span class="relative z-10 text-base sm:text-lg tracking-wide uppercase">
                {{ $t('hero.cta.primary') }}
              </span>
              <ArrowRight :size="24" :stroke-width="2.5" class="relative z-10 group-hover:translate-x-2 transition-transform duration-300" />
            </button>
          </UiMagnetic>

          <!-- Secondary CTA -->
          <UiMagnetic :strength="0.25" :range="100">
            <button
              class="w-full sm:w-auto group relative px-8 py-4 sm:px-12 sm:py-6 rounded-2xl font-bold text-white transition-all duration-500 flex items-center justify-center gap-3 overflow-hidden"
              @click="navigateTo(localePath('/portfolio'))"
            >
              <!-- Border gradient simulation -->
              <div class="absolute inset-0 bg-gradient-to-r from-gray-800 to-gray-700 group-hover:from-gray-700 group-hover:to-gray-600 transition-colors" />
              <div class="absolute inset-[1px] bg-[#0a0a0f] rounded-[15px] z-0" />
              
              <!-- Glow background -->
              <div class="absolute inset-0 opacity-0 group-hover:opacity-10 transition-opacity duration-500 bg-primary-500 blur-xl" />

              <span class="relative z-10 flex items-center gap-3 text-base sm:text-lg tracking-wide uppercase bg-gradient-to-r from-gray-200 to-gray-400 bg-clip-text text-transparent group-hover:from-white group-hover:to-gray-300 transition-all">
                <PlayCircle :size="24" :stroke-width="2" class="text-primary-400 group-hover:rotate-12 transition-transform" />
                {{ $t('hero.cta.secondary') }}
              </span>
            </button>
          </UiMagnetic>
        </div>

        <!-- Stats Grid -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4 sm:gap-8 lg:gap-12 relative">
          <!-- Glass separator lines -->
          <div class="absolute inset-x-0 top-1/2 h-px bg-gradient-to-r from-transparent via-white/5 to-transparent md:hidden" />
          
          <div
            v-for="(stat, index) in animatedStats"
            :key="stat.label"
            v-motion
            :initial="{ opacity: 0, y: 40 }"
            :visibleOnce="{
              opacity: 1,
              y: 0,
              transition: {
                delay: 400 + index * 100,
                duration: 800,
                type: 'spring',
                bounce: 0.4
              }
            }"
            class="group relative"
            @mouseenter="startStatCounter(index)"
          >
            <!-- Decorative card-like background -->
            <div
              class="absolute -inset-2 rounded-2xl opacity-0 group-hover:opacity-100 transition-all duration-500 backdrop-blur-sm border border-white/5"
              :style="{ background: `radial-gradient(120% 120% at 50% 0%, ${getStatColor(index)}10 0%, transparent 80%)` }"
            />

            <!-- Content -->
            <div class="relative text-center py-4 px-2">
              <div
                class="text-4xl sm:text-5xl lg:text-7xl font-black mb-3 bg-gradient-to-br bg-clip-text text-transparent transition-all duration-500 group-hover:scale-110 drop-shadow-sm tabular-nums"
                :style="{ backgroundImage: `linear-gradient(135deg, ${getStatColor(index)} 0%, ${getStatColor((index + 1) % 3)} 100%)` }"
              >
                {{ stat.displayValue }}{{ stat.suffix }}
              </div>

              <div class="text-xs sm:text-sm text-gray-400 font-bold uppercase tracking-[0.2em] group-hover:text-gray-200 transition-colors">
                {{ stat.label }}
              </div>

              <div
                class="absolute -bottom-1 left-1/2 -translate-x-1/2 h-1 w-0 group-hover:w-12 transition-all duration-500 rounded-full blur-[2px]"
                :style="{ backgroundColor: getStatColor(index) }"
              />
            </div>
          </div>
        </div>

        <!-- Scroll indicator -->
        <div class="absolute bottom-10 left-1/2 -translate-x-1/2 hidden lg:block">
          <div class="flex flex-col items-center gap-3 group cursor-pointer" @click="scrollToNextSection">
            <span class="text-[10px] text-gray-500 uppercase tracking-[0.4em] font-bold group-hover:text-primary-400 transition-colors">
              {{ $t('hero.scroll') }}
            </span>
            <div class="w-7 h-12 rounded-full border-2 border-gray-800 flex items-start justify-center p-1.5 transition-colors group-hover:border-primary-500/50">
              <div class="w-1.5 h-3 bg-primary-500 rounded-full animate-scroll-hint" />
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Decorative elements -->
    <div class="absolute inset-0 pointer-events-none select-none">
      <div v-for="i in 15" :key="i"
           class="absolute w-1 h-1 bg-primary-500/30 rounded-full animate-float-particle"
           :style="{
             left: `${Math.random() * 100}%`,
             top: `${Math.random() * 100}%`,
             animationDelay: `${Math.random() * 5}s`,
             animationDuration: `${5 + Math.random() * 10}s`
           }"
      />

      <div class="absolute top-12 right-12 w-48 h-48 border-r-[1px] border-t-[1px] border-secondary-500/10 rounded-tr-[40px] hidden md:block" />
      <div class="absolute bottom-12 left-12 w-48 h-48 border-l-[1px] border-b-[1px] border-primary-500/10 rounded-bl-[40px] hidden md:block" />
    </div>
  </section>
</template>

<script setup lang="ts">
import { ArrowRight, PlayCircle } from 'lucide-vue-next'
import { teamStats } from '~/lib/data'

const { t } = useI18n()
const localePath = useLocalePath()

const animatedStats = ref([
  { target: teamStats.projects, displayValue: 0, suffix: '+', label: t('hero.stats.projects') },
  { target: teamStats.years, displayValue: 0, suffix: '', label: t('hero.stats.years') },
  { target: teamStats.teamSize, displayValue: 0, suffix: '+', label: t('hero.stats.team') },
  { target: teamStats.clients, displayValue: 0, suffix: '+', label: t('hero.stats.clients') }
])

function startStatCounter(index: number) {
  const stat = animatedStats.value[index]
  if (!stat || stat.displayValue === stat.target) return
  
  let current = 0
  const duration = 1500
  const stepTime = Math.max(10, Math.floor(duration / stat.target))
  
  const timer = setInterval(() => {
    current += 1
    stat.displayValue = current
    if (current >= stat.target) {
      stat.displayValue = stat.target
      clearInterval(timer)
    }
  }, stepTime)
}

onMounted(() => {
  setTimeout(() => {
    animatedStats.value.forEach((_, i) => startStatCounter(i))
  }, 800)
})

const getStatColor = (index: number): string => {
  const colors = ['#0ea5e9', '#8b5cf6', '#06b6d4', '#4f46e5']
  return colors[index % colors.length] || '#0ea5e9'
}

function scrollToNextSection() {
  window.scrollTo({
    top: window.innerHeight,
    behavior: 'smooth'
  })
}
</script>

<style scoped>
@keyframes glow-spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

@keyframes flare {
  0% { transform: translate(-50%, -50%) rotate(0deg) scale(1); opacity: 0; }
  50% { opacity: 0.5; }
  100% { transform: translate(50%, 50%) rotate(45deg) scale(2); opacity: 0; }
}

.animate-flare {
  animation: flare 1.5s ease-out infinite;
}

@keyframes scroll-hint {
  0% { transform: translateY(0); opacity: 1; }
  50% { transform: translateY(15px); opacity: 0.2; }
  100% { transform: translateY(0); opacity: 1; }
}

.animate-scroll-hint {
  animation: scroll-hint 2s ease-in-out infinite;
}

@keyframes float-particle {
  0%, 100% { transform: translate(0, 0); }
  33% { transform: translate(30px, -20px); }
  66% { transform: translate(-20px, 40px); }
}

.animate-float-particle {
  animation: float-particle 10s ease-in-out infinite;
}

.bg-gradient-radial {
  background: radial-gradient(circle at center, var(--tw-gradient-stops));
}

.container-custom {
  max-width: 1400px;
  margin-left: auto;
  margin-right: auto;
  padding-left: 1rem;
  padding-right: 1rem;
}

@media (min-width: 640px) {
  .container-custom {
    padding-left: 1.5rem;
    padding-right: 1.5rem;
  }
}

@media (min-width: 1024px) {
  .container-custom {
    padding-left: 3rem;
    padding-right: 3rem;
  }
}

.tabular-nums {
  font-variant-numeric: tabular-nums;
}
</style>
