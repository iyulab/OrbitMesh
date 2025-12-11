import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/agent': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
      '/hub': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true,
      },
      '/swagger': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
  // Ensure SPA routing works - Vite handles this by default but be explicit
  appType: 'spa',
})
