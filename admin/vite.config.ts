import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
// The extension is spelled out because Vite's native config loader runs this file under Node,
// which resolves a relative import only by its full file name.
import { themeBootPlugin } from './src/lib/themeBootPlugin.ts'

export default defineConfig({
  // Relative: the deployed base path is set at container start (<base href> + config.js).
  base: './',
  plugins: [
    vue(),
    tailwindcss(),
    themeBootPlugin(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    // Every route is already a lazy chunk and the country atlas is imported from its
    // `/lite` entry, so the only chunk over the default 500 kB is the world-map outline
    // data (@svg-maps/world): one 1.2 MB object literal of SVG paths that cannot be cut
    // into pieces. It loads only on the clients-by-country report, in its own named chunk.
    // The limit sits just above it so any NEW oversized chunk still warns.
    chunkSizeWarningLimit: 1300,
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [{ name: 'world-map', test: /@svg-maps.world/ }],
        },
      },
    },
  },
  server: {
    watch: {
      // The dev compose file turns on polling (CHOKIDAR_USEPOLLING) because a Windows bind mount
      // delivers no file events. Polling stats every watched file on a timer over a slow 9p mount, and
      // the defaults (100 ms for text, 300 ms for binary files) over the whole project kept the dev
      // server's event loop so busy that every page load waited ~40 s. Two cuts fix it at the root:
      // - folders the browser never loads (test sources, e2e, the build output, reports) are not
      //   watched at all, which removes more than half of the watched files;
      // - the remaining source files are polled once a second, still instant for hot reload.
      ignored: ['**/tests/**', '**/e2e/**', '**/dist/**', '**/coverage/**', '**/playwright-report/**'],
      interval: 1000,
      binaryInterval: 3000,
    },
    // Extra names the dev server answers to besides localhost, from the environment (ADMIN_DEV_ALLOWED_HOSTS,
    // comma separated): a deployment's own host names are not written into the repository.
    allowedHosts: (process.env.ADMIN_DEV_ALLOWED_HOSTS ?? '').split(',').map((h) => h.trim()).filter(Boolean),
    proxy: {
      '/api': {
        target: process.env.API_PROXY_TARGET || 'http://localhost:5148',
        changeOrigin: true,
      },
      '/uploads': {
        target: process.env.API_PROXY_TARGET || 'http://localhost:5148',
        changeOrigin: true,
      },
    },
  },
})
