import type { Conversation, Message, FileAttachment, ToolDefinition, GitRepository, ConversationRepo, GitCredentialType, BuildResult } from '../types';
import { IfLoggerProvider } from '@if/web-common-react';

const logger = IfLoggerProvider.createLogger('ApiService');

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

    // Log context extraction status for debugging
    logger.info(`File upload complete: ${result.attachmentCount} attachment(s), ` +
      `contextStored=${result.contextStored}, contextLength=${result.contextLength}`);
    logger.debug(`File upload details: ${JSON.stringify(result.attachments?.map((a: Record<string, unknown>) => ({
      fileName: a.fileName, fileType: a.fileType, sizeBytes: a.sizeBytes
    })))}`);

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

  async getContextStatus(conversationId: string, userId: string): Promise<{ hasContext: boolean; lengthChars: number }> {
    return this.fetch<{ hasContext: boolean; lengthChars: number }>(
      `/api/conversations/${conversationId}/context?userId=${encodeURIComponent(userId)}`
    );
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

  // ── Repositories ────────────────────────────────────────────────

  async getRepositories(userId: string): Promise<GitRepository[]> {
    return this.fetch<GitRepository[]>(`/api/repositories?userId=${encodeURIComponent(userId)}`);
  }

  async cloneRepository(
    userId: string,
    name: string,
    url: string,
    credentialType: GitCredentialType,
    credential: string
  ): Promise<GitRepository> {
    return this.fetch<GitRepository>(`/api/repositories/clone?userId=${encodeURIComponent(userId)}`, {
      method: 'POST',
      body: JSON.stringify({ name, url, credentialType, credential }),
    });
  }

  async pullRepository(repoId: string, userId: string): Promise<void> {
    await this.fetch(`/api/repositories/${repoId}/pull?userId=${encodeURIComponent(userId)}`, {
      method: 'POST',
    });
  }

  async deleteRepository(repoId: string, userId: string): Promise<void> {
    await this.fetch(`/api/repositories/${repoId}?userId=${encodeURIComponent(userId)}`, {
      method: 'DELETE',
    });
  }

  // ── Conversation-repo linking ───────────────────────────────────

  async getConversationRepos(conversationId: string, userId: string): Promise<ConversationRepo[]> {
    return this.fetch<ConversationRepo[]>(
      `/api/repositories/conversation/${conversationId}?userId=${encodeURIComponent(userId)}`
    );
  }

  async linkRepoToConversation(
    conversationId: string,
    repositoryId: string,
    conversationTitle: string,
    userId: string
  ): Promise<ConversationRepo> {
    return this.fetch<ConversationRepo>(
      `/api/repositories/conversation/${conversationId}/link?userId=${encodeURIComponent(userId)}`,
      {
        method: 'POST',
        body: JSON.stringify({ repositoryId, conversationTitle }),
      }
    );
  }

  async setRepoEnabled(
    conversationId: string,
    repoId: string,
    enabled: boolean,
    userId: string
  ): Promise<void> {
    await this.fetch(
      `/api/repositories/conversation/${conversationId}/${repoId}/enabled?userId=${encodeURIComponent(userId)}`,
      {
        method: 'PATCH',
        body: JSON.stringify({ enabled }),
      }
    );
  }

  async unlinkRepo(conversationId: string, repoId: string, userId: string): Promise<void> {
    await this.fetch(
      `/api/repositories/conversation/${conversationId}/${repoId}?userId=${encodeURIComponent(userId)}`,
      { method: 'DELETE' }
    );
  }

  // ── Build, commit, merge ────────────────────────────────────────

  async buildRepo(repoId: string, userId: string, projectPath?: string): Promise<BuildResult> {
    return this.fetch<BuildResult>(
      `/api/repositories/${repoId}/build?userId=${encodeURIComponent(userId)}`,
      {
        method: 'POST',
        body: JSON.stringify({ projectPath: projectPath ?? null }),
      }
    );
  }

  async commitRepo(repoId: string, message: string, userId: string): Promise<{ success: boolean; output: string }> {
    return this.fetch<{ success: boolean; output: string }>(
      `/api/repositories/${repoId}/commit?userId=${encodeURIComponent(userId)}`,
      {
        method: 'POST',
        body: JSON.stringify({ message }),
      }
    );
  }

  async mergeAndPush(
    conversationId: string,
    repoId: string,
    userId: string
  ): Promise<{ success: boolean; message: string }> {
    return this.fetch<{ success: boolean; message: string }>(
      `/api/repositories/conversation/${conversationId}/${repoId}/merge?userId=${encodeURIComponent(userId)}`,
      { method: 'POST' }
    );
  }

  async getRepoStatus(repoId: string, userId: string): Promise<{ status: string }> {
    return this.fetch<{ status: string }>(
      `/api/repositories/${repoId}/status?userId=${encodeURIComponent(userId)}`
    );
  }

  async getRepoFileTree(repoId: string, userId: string): Promise<{ tree: string }> {
    return this.fetch<{ tree: string }>(
      `/api/repositories/${repoId}/tree?userId=${encodeURIComponent(userId)}`
    );
  }
}

export const apiService = new ApiService();
