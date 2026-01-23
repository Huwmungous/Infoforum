export interface ConfigEntry {
  idx: number;
  realm: string;
  client: string;
  userConfig: Record<string, unknown> | null;
  serviceConfig: Record<string, unknown> | null;
  bootstrapConfig: Record<string, unknown> | null;
  enabled: boolean;
}

export interface ConfigEntryDto {
  realm: string;
  client: string;
  userConfig: string;
  serviceConfig: string;
  bootstrapConfig: string;
  enabled: boolean;
}

export interface BatchResponse {
  entries: ConfigEntry[];
  total: number;
  offset: number;
  limit: number;
}

export interface ErrorResponse {
  error: string;
}
