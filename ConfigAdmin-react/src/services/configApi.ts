import type { ConfigEntry, ConfigEntryDto, BatchResponse } from '../types/config';

const API_BASE = '/ConfigDb';

class ConfigApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
    this.name = 'ConfigApiError';
  }
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
    throw new ConfigApiError(response.status, errorData.error || `HTTP ${response.status}`);
  }
  return response.json();
}

export const configApi = {
  async getBatch(offset = 0, limit = 100, includeDisabled = true): Promise<BatchResponse> {
    const params = new URLSearchParams({
      offset: offset.toString(),
      limit: limit.toString(),
      includeDisabled: includeDisabled.toString()
    });
    const response = await fetch(`${API_BASE}/batch?${params}`);
    return handleResponse<BatchResponse>(response);
  },

  async getByIdx(idx: number): Promise<ConfigEntry> {
    const response = await fetch(`${API_BASE}/${idx}`);
    return handleResponse<ConfigEntry>(response);
  },

  async create(entry: ConfigEntryDto): Promise<ConfigEntry> {
    const response = await fetch(API_BASE, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(entry)
    });
    return handleResponse<ConfigEntry>(response);
  },

  async update(idx: number, entry: ConfigEntryDto): Promise<void> {
    const response = await fetch(`${API_BASE}/${idx}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(entry)
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw new ConfigApiError(response.status, errorData.error || `HTTP ${response.status}`);
    }
  },

  async setEnabled(idx: number, enabled: boolean): Promise<void> {
    const response = await fetch(`${API_BASE}/${idx}/enabled`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled })
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw new ConfigApiError(response.status, errorData.error || `HTTP ${response.status}`);
    }
  },

  async delete(idx: number): Promise<void> {
    const response = await fetch(`${API_BASE}/${idx}`, {
      method: 'DELETE'
    });
    if (!response.ok) {
      const errorData = await response.json().catch(() => ({ error: 'Unknown error' }));
      throw new ConfigApiError(response.status, errorData.error || `HTTP ${response.status}`);
    }
  }
};

export { ConfigApiError };
