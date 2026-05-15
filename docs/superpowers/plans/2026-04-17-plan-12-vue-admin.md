# Vue Admin Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Vue.js admin panel from scratch — project setup with Vite + Vue 3 + TypeScript + Pinia + Vue Router + Tailwind CSS + shadcn-vue, auth module with login and JWT storage in httpOnly cookie via proxy, dashboard module with stats cards and charts, all CRUD modules (clients, billing, services, domains, support, settings), composables for API calls and auth, Pinia stores for each module, Vue Router with auth guards, proxy setup for dev and prod.

**Architecture:** SPA with Vue Router; auth via proxy pattern (Browser → Vue Dev Server → C# API); JWT in httpOnly cookie; shadcn-vue for UI components; Pinia for state management; Tailwind CSS for styling; responsive layout with sidebar navigation.

**Tech Stack:** Vite, Vue 3, TypeScript, Pinia, Vue Router, Tailwind CSS, shadcn-vue, Axios/Fetch, Chart.js or Recharts for charts.

---

## File Map

```
innovayse-admin/
├── src/
│   ├── assets/
│   │   └── logo.svg
│   ├── components/
│   │   ├── layout/
│   │   │   ├── AppLayout.vue
│   │   │   ├── AppSidebar.vue
│   │   │   ├── AppHeader.vue
│   │   │   └── AppFooter.vue
│   │   └── ui/                                     ← shadcn-vue components
│   │       ├── Button.vue
│   │       ├── Card.vue
│   │       ├── Input.vue
│   │       ├── Table.vue
│   │       ├── Modal.vue
│   │       └── ...
│   ├── composables/
│   │   ├── useApi.ts                               ← Axios wrapper with proxy
│   │   └── useAuth.ts                              ← Auth state + login/logout
│   ├── modules/
│   │   ├── auth/
│   │   │   ├── views/
│   │   │   │   └── LoginView.vue
│   │   │   └── stores/
│   │   │       └── authStore.ts
│   │   ├── dashboard/
│   │   │   ├── views/
│   │   │   │   └── DashboardView.vue
│   │   │   ├── components/
│   │   │   │   ├── StatsCard.vue
│   │   │   │   └── RevenueChart.vue
│   │   │   └── stores/
│   │   │       └── dashboardStore.ts
│   │   ├── clients/
│   │   │   ├── views/
│   │   │   │   ├── ClientsListView.vue
│   │   │   │   ├── ClientDetailsView.vue
│   │   │   │   └── ClientEditView.vue
│   │   │   └── stores/
│   │   │       └── clientsStore.ts
│   │   ├── billing/
│   │   │   ├── views/
│   │   │   │   ├── InvoicesListView.vue
│   │   │   │   └── InvoiceDetailsView.vue
│   │   │   └── stores/
│   │   │       └── billingStore.ts
│   │   ├── services/
│   │   │   ├── views/
│   │   │   │   ├── ServicesListView.vue
│   │   │   │   └── ServiceDetailsView.vue
│   │   │   └── stores/
│   │   │       └── servicesStore.ts
│   │   ├── domains/
│   │   │   ├── views/
│   │   │   │   ├── DomainsListView.vue
│   │   │   │   └── DomainDetailsView.vue
│   │   │   └── stores/
│   │   │       └── domainsStore.ts
│   │   ├── support/
│   │   │   ├── views/
│   │   │   │   ├── TicketsListView.vue
│   │   │   │   └── TicketDetailsView.vue
│   │   │   └── stores/
│   │   │       └── supportStore.ts
│   │   └── settings/
│   │       ├── views/
│   │       │   ├── EmailTemplatesView.vue
│   │       │   ├── DepartmentsView.vue
│   │       │   └── SystemSettingsView.vue
│   │       └── stores/
│   │           └── settingsStore.ts
│   ├── router/
│   │   └── index.ts                                ← Vue Router with auth guards
│   ├── stores/
│   │   └── index.ts                                ← Pinia setup
│   ├── types/
│   │   ├── api.ts                                  ← API response types
│   │   └── models.ts                               ← Domain models
│   ├── App.vue
│   └── main.ts
├── public/
│   └── favicon.ico
├── .env.development
├── .env.production
├── index.html
├── package.json
├── tailwind.config.js
├── tsconfig.json
└── vite.config.ts                                  ← Proxy config for dev
```

---

## Task 1: Project Setup

- [ ] **Step 1: Create Vite project**

```bash
npm create vite@latest innovayse-admin -- --template vue-ts
cd innovayse-admin
npm install
```

- [ ] **Step 2: Install dependencies**

```bash
npm install vue-router@4 pinia axios
npm install -D tailwindcss postcss autoprefixer
npm install -D @types/node
npx tailwindcss init -p
```

- [ ] **Step 3: Setup Tailwind CSS**

```js
// tailwind.config.js
export default {
  content: [
    "./index.html",
    "./src/**/*.{vue,js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
}
```

```css
/* src/assets/main.css */
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 4: Setup shadcn-vue**

```bash
npx shadcn-vue@latest init
```

Add components:
```bash
npx shadcn-vue@latest add button
npx shadcn-vue@latest add card
npx shadcn-vue@latest add input
npx shadcn-vue@latest add table
npx shadcn-vue@latest add dialog
```

- [ ] **Step 5: Configure Vite proxy**

```typescript
// vite.config.ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/proxy': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/proxy/, '/api')
      }
    }
  }
})
```

- [ ] **Step 6: Create environment files**

```
# .env.development
VITE_API_URL=/proxy

# .env.production
VITE_API_URL=/proxy
```

- [ ] **Step 7: Commit**

```bash
git init
git add .
git commit -m "feat: initial Vue admin panel setup with Vite, TypeScript, Tailwind, and shadcn-vue"
```

---

## Task 2: Composables (useApi, useAuth)

- [ ] **Step 1: Create `composables/useApi.ts`**

```typescript
// src/composables/useApi.ts
import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios'

/**
 * Axios instance configured for API calls via proxy.
 *
 * All requests go through /proxy which is rewritten to /api by Vite dev server
 * or Nginx in production. The JWT is stored in an httpOnly cookie, so it's
 * automatically included in all requests.
 */
const apiClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/proxy',
  withCredentials: true, // Send cookies with requests
  headers: {
    'Content-Type': 'application/json'
  }
})

/**
 * Composable for making API calls.
 *
 * @returns API methods (get, post, put, delete)
 */
export function useApi() {
  async function get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await apiClient.get<T>(url, config)
    return response.data
  }

  async function post<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    const response = await apiClient.post<T>(url, data, config)
    return response.data
  }

  async function put<T>(url: string, data?: any, config?: AxiosRequestConfig): Promise<T> {
    const response = await apiClient.put<T>(url, data, config)
    return response.data
  }

  async function del<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    const response = await apiClient.delete<T>(url, config)
    return response.data
  }

  return {
    get,
    post,
    put,
    delete: del
  }
}
```

- [ ] **Step 2: Create `composables/useAuth.ts`**

```typescript
// src/composables/useAuth.ts
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from './useApi'

interface User {
  id: number
  email: string
  firstName: string
  lastName: string
  role: string
}

const user = ref<User | null>(null)
const loading = ref(false)

/**
 * Composable for authentication.
 *
 * @returns Auth state and methods
 */
export function useAuth() {
  const api = useApi()
  const router = useRouter()

  async function login(email: string, password: string) {
    loading.value = true
    try {
      await api.post('/auth/login', { email, password })
      await fetchUser()
      router.push('/dashboard')
    } finally {
      loading.value = false
    }
  }

  async function logout() {
    await api.post('/auth/logout')
    user.value = null
    router.push('/login')
  }

  async function fetchUser() {
    try {
      user.value = await api.get<User>('/clients/me')
    } catch {
      user.value = null
    }
  }

  return {
    user,
    loading,
    login,
    logout,
    fetchUser
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/composables/
git commit -m "feat: add useApi and useAuth composables"
```

---

## Task 3: Pinia Stores

- [ ] **Step 1: Setup Pinia**

```typescript
// src/stores/index.ts
import { createPinia } from 'pinia'

export const pinia = createPinia()
```

- [ ] **Step 2: Create auth store**

```typescript
// src/modules/auth/stores/authStore.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useAuth } from '@/composables/useAuth'

export const useAuthStore = defineStore('auth', () => {
  const { user, loading, login, logout, fetchUser } = useAuth()

  return {
    user,
    loading,
    login,
    logout,
    fetchUser
  }
})
```

- [ ] **Step 3: Create dashboard store**

```typescript
// src/modules/dashboard/stores/dashboardStore.ts
import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useApi } from '@/composables/useApi'

interface DashboardStats {
  totalRevenue: number
  monthlyRevenue: number
  activeServices: number
  overdueInvoices: number
  openTickets: number
  totalClients: number
}

export const useDashboardStore = defineStore('dashboard', () => {
  const api = useApi()
  const stats = ref<DashboardStats | null>(null)
  const loading = ref(false)

  async function fetchStats() {
    loading.value = true
    try {
      stats.value = await api.get<DashboardStats>('/admin/dashboard/stats')
    } finally {
      loading.value = false
    }
  }

  return {
    stats,
    loading,
    fetchStats
  }
})
```

- [ ] **Step 4: Create stores for other modules (clients, billing, services, domains, support, settings)**

Follow the same pattern for each module.

- [ ] **Step 5: Commit**

```bash
git add src/stores/ src/modules/*/stores/
git commit -m "feat: add Pinia stores for all modules"
```

---

## Task 4: Vue Router Setup

- [ ] **Step 1: Create router**

```typescript
// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/modules/auth/stores/authStore'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/modules/auth/views/LoginView.vue'),
      meta: { requiresAuth: false }
    },
    {
      path: '/',
      redirect: '/dashboard',
      meta: { requiresAuth: true }
    },
    {
      path: '/dashboard',
      name: 'Dashboard',
      component: () => import('@/modules/dashboard/views/DashboardView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/clients',
      name: 'Clients',
      component: () => import('@/modules/clients/views/ClientsListView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/clients/:id',
      name: 'ClientDetails',
      component: () => import('@/modules/clients/views/ClientDetailsView.vue'),
      meta: { requiresAuth: true }
    },
    // Add routes for billing, services, domains, support, settings
  ]
})

// Auth guard
router.beforeEach(async (to, from, next) => {
  const authStore = useAuthStore()

  if (to.meta.requiresAuth && !authStore.user) {
    await authStore.fetchUser()
    if (!authStore.user) {
      next('/login')
    } else {
      next()
    }
  } else {
    next()
  }
})

export default router
```

- [ ] **Step 2: Commit**

```bash
git add src/router/
git commit -m "feat: add Vue Router with auth guards"
```

---

## Task 5: Layout Components

- [ ] **Step 1: Create AppLayout.vue**

```vue
<!-- src/components/layout/AppLayout.vue -->
<template>
  <div class="flex h-screen bg-gray-100">
    <AppSidebar />
    <div class="flex flex-col flex-1 overflow-hidden">
      <AppHeader />
      <main class="flex-1 overflow-y-auto p-6">
        <slot />
      </main>
      <AppFooter />
    </div>
  </div>
</template>

<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
import AppHeader from './AppHeader.vue'
import AppFooter from './AppFooter.vue'
</script>
```

- [ ] **Step 2: Create AppSidebar, AppHeader, AppFooter components**

Create sidebar with navigation links, header with user menu, and footer.

- [ ] **Step 3: Commit**

```bash
git add src/components/layout/
git commit -m "feat: add layout components (sidebar, header, footer)"
```

---

## Task 6: Auth Module

- [ ] **Step 1: Create LoginView.vue**

```vue
<!-- src/modules/auth/views/LoginView.vue -->
<template>
  <div class="flex items-center justify-center min-h-screen bg-gray-100">
    <Card class="w-full max-w-md p-6">
      <h1 class="text-2xl font-bold mb-6">Admin Login</h1>
      <form @submit.prevent="handleLogin">
        <div class="mb-4">
          <Input v-model="email" type="email" placeholder="Email" required />
        </div>
        <div class="mb-6">
          <Input v-model="password" type="password" placeholder="Password" required />
        </div>
        <Button type="submit" :disabled="loading" class="w-full">
          {{ loading ? 'Logging in...' : 'Login' }}
        </Button>
      </form>
    </Card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../stores/authStore'
import { Card, Input, Button } from '@/components/ui'

const authStore = useAuthStore()
const email = ref('')
const password = ref('')
const loading = ref(false)

async function handleLogin() {
  loading.value = true
  try {
    await authStore.login(email.value, password.value)
  } catch (error) {
    alert('Login failed')
  } finally {
    loading.value = false
  }
}
</script>
```

- [ ] **Step 2: Commit**

```bash
git add src/modules/auth/
git commit -m "feat: add login view"
```

---

## Task 7: Dashboard Module

- [ ] **Step 1: Create DashboardView.vue with stats cards and charts**

```vue
<!-- src/modules/dashboard/views/DashboardView.vue -->
<template>
  <AppLayout>
    <h1 class="text-3xl font-bold mb-6">Dashboard</h1>

    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-6">
      <StatsCard title="Total Revenue" :value="formatCurrency(stats?.totalRevenue)" />
      <StatsCard title="Monthly Revenue" :value="formatCurrency(stats?.monthlyRevenue)" />
      <StatsCard title="Active Services" :value="stats?.activeServices" />
      <StatsCard title="Overdue Invoices" :value="stats?.overdueInvoices" />
      <StatsCard title="Open Tickets" :value="stats?.openTickets" />
      <StatsCard title="Total Clients" :value="stats?.totalClients" />
    </div>

    <Card>
      <RevenueChart />
    </Card>
  </AppLayout>
</template>

<script setup lang="ts">
import { onMounted, computed } from 'vue'
import { useDashboardStore } from '../stores/dashboardStore'
import AppLayout from '@/components/layout/AppLayout.vue'
import StatsCard from '../components/StatsCard.vue'
import RevenueChart from '../components/RevenueChart.vue'
import { Card } from '@/components/ui'

const dashboardStore = useDashboardStore()
const stats = computed(() => dashboardStore.stats)

onMounted(() => {
  dashboardStore.fetchStats()
})

function formatCurrency(value?: number) {
  if (!value) return '$0.00'
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value)
}
</script>
```

- [ ] **Step 2: Create StatsCard and RevenueChart components**
- [ ] **Step 3: Commit**

```bash
git add src/modules/dashboard/
git commit -m "feat: add dashboard with stats cards and revenue chart"
```

---

## Task 8: CRUD Modules (Clients, Billing, Services, Domains, Support, Settings)

For each module, create:
- List view with table
- Details view
- Edit/Create modals or views
- Pinia store with CRUD actions

Follow the same patterns established in dashboard and auth modules.

- [ ] **Step 1: Create clients module**
- [ ] **Step 2: Create billing module**
- [ ] **Step 3: Create services module**
- [ ] **Step 4: Create domains module**
- [ ] **Step 5: Create support module**
- [ ] **Step 6: Create settings module**
- [ ] **Step 7: Commit**

```bash
git add src/modules/
git commit -m "feat: add all CRUD modules (clients, billing, services, domains, support, settings)"
```

---

## Task 9: Production Setup

- [ ] **Step 1: Create Nginx config for production**

```nginx
# /etc/nginx/sites-available/admin.innovayse.com
server {
    listen 80;
    server_name admin.innovayse.com;

    root /var/www/innovayse-admin/dist;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location /proxy/ {
        proxy_pass http://localhost:5000/api/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

- [ ] **Step 2: Create build script**

```json
// package.json
{
  "scripts": {
    "dev": "vite",
    "build": "vue-tsc && vite build",
    "preview": "vite preview"
  }
}
```

- [ ] **Step 3: Build for production**

```bash
npm run build
```

- [ ] **Step 4: Deploy to server**

```bash
rsync -avz dist/ user@server:/var/www/innovayse-admin/dist/
```

---

## Self-Review

- [x] Project setup with Vite, Vue 3, TypeScript, Tailwind, shadcn-vue
- [x] Composables (useApi, useAuth)
- [x] Pinia stores for all modules
- [x] Vue Router with auth guards
- [x] Layout components (sidebar, header, footer)
- [x] Auth module (login)
- [x] Dashboard module (stats, charts)
- [x] All CRUD modules
- [x] Proxy setup for dev and prod
- [x] Production build and deployment

---

## Execution Handoff

Plan complete. Choose execution method.
