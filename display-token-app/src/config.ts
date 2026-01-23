// src/config.ts

// Helper function to safely access Vite environment variables
function getEnvVar(key: string, defaultValue: string): string {
  // @ts-ignore: import.meta may not exist in some TS contexts
  return (import.meta && import.meta.env && import.meta.env[key]) ?? defaultValue;
}

export const AppConfig = {
  configUrl: getEnvVar('VITE_IF_CONFIG_SERVICE', 'https://ifdevelopment.com/config'),
  realm: getEnvVar('VITE_IF_REALM', 'IfDevelopment_Dev'),
  client: getEnvVar('VITE_IF_CLIENT', 'dev-login')
};
