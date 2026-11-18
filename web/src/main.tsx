import '@fontsource-variable/inter'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App'
import { apiHttp } from './api/client'
import './styles.css'

// En mode « demo » (npm run build:demo), l'API est simulée dans la page : aucune donnée réelle, aucun serveur.
const api = import.meta.env.MODE === 'demo' ? (await import('./api/demo')).apiDemo : apiHttp

createRoot(document.getElementById('racine')!).render(
  <StrictMode>
    <App api={api} />
  </StrictMode>,
)
