import { createApp } from 'vue'
import { pinia } from './pinia'
import App from './App.vue'
import router from './router'
import { i18n } from './i18n'
import './assets/main.css'

// Nothing is restored from browser storage here any more, in either mode. Local-mode
// sessions used to need a token read back out of sessionStorage before the router's
// first guard ran; the API now writes that token into an httpOnly cookie the browser
// attaches on its own, so the guard's /auth/me call carries the session without this
// app having to know a credential exists.

const app = createApp(App)
app.use(pinia)
app.use(router)
app.use(i18n)
app.mount('#app')
