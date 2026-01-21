/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_SFD_CONFIG_SERVICE: string;
  readonly VITE_SFD_REALM: string;
  readonly VITE_SFD_CLIENT: string;
  // add other VITE_ variables here if needed
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
