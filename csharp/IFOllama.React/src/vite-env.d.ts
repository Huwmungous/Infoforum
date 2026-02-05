/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_IF_CONFIG_SERVICE_URL: string;
  readonly VITE_IF_APP_DOMAIN: string;
  readonly VITE_IF_APP_NAME: string;
  readonly VITE_IF_ENVIRONMENT: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
