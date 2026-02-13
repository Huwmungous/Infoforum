import type { Conversation, Message, FileAttachment, ToolDefinition } from '../types';

const API_BASE_URL = import.meta.env.VITE_API_URL || '';

class ApiService {
  private accessToken: string | null = null;

  setAccessToken(token: string | null): void {
    this.accessToken = token;
  }

  private async fetch<T>(url: string, options: RequestInit = {}): Promise<T> {
    const headers: HeadersInit = {
      'Content-Type': 'application/json',
      ...options.headers,
    };

    if (this.accessToken) {
      (headers as Record<string, string>)['Authorization'] = `Bearer ${this.accessToken}`;
    }

    const response = await fetch(`${API_BASE_URL}${url}`, {
      ...options,
      headers,
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`API Error: ${response.status} - ${errorText}`);
    }

    return response.json();
  }

  // Conversations
  async getConversations(userId: string): Promise<Conversation[]> {
    return this.fetch<Conversation[]>(`/api/conversations?userId=${encodeURIComponent(userId)}`);
  }

  async createConversation(title: string, userId: string): Promise<Conversation> {
    return this.fetch<Conversation>(`/api/conversations?userId=${encodeURIComponent(userId)}`, {
      method: 'POST',
      body: JSON.stringify(title),
    });
  }

  async getMessages(conversationId: string, userId: string): Promise<Message[]> {
    return this.fetch<Message[]>(
      `/api/conversations/${conversationId}/messages?userId=${encodeURIComponent(userId)}`
    );
  }

  async appendMessage(conversationId: string, message: Message, userId: string): Promise<void> {
    await this.fetch(`/api/conversations/${conversationId}/messages?userId=${encodeURIComponent(userId)}`, {
      method: 'POST',
      body: JSON.stringify(message),
    });
  }

  async appendMessageWithFiles(
    conversationId: string,
    content: string,
    files: File[],
    userId: string
  ): Promise<FileAttachment[]> {
    const formData = new FormData();
    formData.append('role', 'user');
    formData.append('content', content);
    for (const file of files) {
      formData.append('files', file);
    }

    const headers: HeadersInit = {};
    if (this.accessToken) {
      headers['Authorization'] = `Bearer ${this.accessToken}`;
    }

    const response = await fetch(
      `${API_BASE_URL}/api/conversations/${conversationId}/messages/with-files?userId=${encodeURIComponent(userId)}`,
      { method: 'POST', headers, body: formData }
    );

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`API Error: ${response.status} - ${errorText}`);
    }

    const result = await response.json();
    return result.attachments ?? [];
  }

  async deleteConversation(conversationId: string, userId: string): Promise<void> {
    await this.fetch(`/api/conversations/${conversationId}?userId=${encodeURIComponent(userId)}`, {
      method: 'DELETE',
    });
  }

  async generateTitle(conversationId: string, userMessage: string, userId: string): Promise<string> {
    const result = await this.fetch<{ title: string }>(
      `/api/conversations/${conversationId}/generate-title?userId=${encodeURIComponent(userId)}`,
      {
        method: 'POST',
        body: JSON.stringify(userMessage),
      }
    );
    return result.title;
  }

  async updateTitle(conversationId: string, title: string, userId: string): Promise<string> {
    const result = await this.fetch<{ title: string }>(
      `/api/conversations/${conversationId}/title?userId=${encodeURIComponent(userId)}`,
      {
        method: 'PATCH',
        body: JSON.stringify(title),
      }
    );
    return result.title;
  }

  // MCP Tools
  async getTools(): Promise<{ tools: ToolDefinition[]; count: number }> {
    return this.fetch<{ tools: ToolDefinition[]; count: number }>('/api/mcp/tools');
  }

  async getServers(): Promise<{ servers: Record<string, string>; count: number }> {
    return this.fetch<{ servers: Record<string, string>; count: number }>('/api/mcp/servers');
  }

  // Health check
  async healthCheck(): Promise<{ status: string; service: string; timestamp: string }> {
    return this.fetch<{ status: string; service: string; timestamp: string }>('/api/diag/health');
  }
}

export const apiService = new ApiService();
