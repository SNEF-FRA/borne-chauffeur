import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { viteSingleFile } from 'vite-plugin-singlefile'

// Mode normal : fichiers servis par l'API (dossier wwwroot).
// Mode "demo" : un seul fichier HTML autonome, sans serveur, avec des données fictives.
export default defineConfig(({ mode }) => ({
  plugins: mode === 'demo' ? [react(), viteSingleFile()] : [react()],
  build: {
    outDir: mode === 'demo' ? 'dist-demo' : 'dist',
    assetsInlineLimit: mode === 'demo' ? 100_000_000 : 4096,
  },
  server: {
    proxy: { '/api': 'http://127.0.0.1:5080' },
  },
}))
