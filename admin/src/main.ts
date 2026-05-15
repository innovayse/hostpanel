import { createApp } from 'vue'
import { pinia } from './pinia'
import App from './App.vue'
import router from './router'
import './assets/main.css'

const app = createApp(App)
app.use(pinia)
app.use(router)
app.mount('#app')
