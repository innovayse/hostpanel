module.exports = {
  apps: [{
    name: 'innovayse-nuxt',
    script: './.output/server/index.mjs',
    cwd: '/var/www/innovayse',
    env: {
      NODE_ENV: 'production',
      NITRO_PORT: 3000,
      NITRO_HOST: '0.0.0.0',
      // WHMCS API Configuration - using NUXT_ prefix for runtime config
      NUXT_WHMCS_URL: 'http://173.212.198.16:8080',
      NUXT_WHMCS_IDENTIFIER: 'pqw2pzUl9lxinjjuHp0kbMwWTQ5gN6O3',
      NUXT_WHMCS_SECRET: 'QMFqZ8MML2yAetwNlv50QlweWBk0AVbt',
      NUXT_WHMCS_ACCESS_KEY: '',
      // Public config
      NUXT_PUBLIC_BASE_URL: 'https://innovayse.com'
    },
    instances: 1,
    exec_mode: 'fork',
    autorestart: true,
    watch: false,
    max_memory_restart: '1G'
  }]
}