import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // The ASP.NET Core API (backend/README.md) runs on this port in development.
    proxy: { '/api': 'http://localhost:5080' },
  },
})
