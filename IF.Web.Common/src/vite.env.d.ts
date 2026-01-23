/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_IF_CONFIG_SERVICE: string
  readonly VITE_IF_REALM: string
  readonly VITE_IF_CLIENT: string
  // add other VITE_ variables here if needed
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
