import { defineConfig } from 'vite'

export default defineConfig({
  server: {
    proxy: {
      '/global-halal-api': {
        target: 'https://apps.globalhalalhub.co.id',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})
