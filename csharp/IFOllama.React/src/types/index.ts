export interface Message {
  id: string;
  role: 'user' | 'assistant' | 'system' | 'tool';
  content: string;
  timestamp: Date;
  thinkContent?: string;
  showThinking?: boolean;
  attachments?: FileAttachment[];
}

export interface FileAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  storagePath: string;
  uploadedAt: Date;
  fileType: FileContentType;
}

export enum FileContentType {
  Unknown = 0,
  Image = 1,
  Document = 2,
  Text = 3,
  Pdf = 4,
  Zip = 5,
}

export interface Conversation {
  id: string;
  title: string;
  userId: string;
}

export interface ToolCategory {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
}

export interface ToolDefinition {
  serverName: string;
  name: string;
  description: string;
  inputSchema: unknown;
}

export interface ChatState {
  messages: Message[];
  isLoading: boolean;
  error: string | null;
  conversationId: string | null;
}

export interface AuthConfig {
  authority: string;
  clientId: string;
  redirectUri: string;
  postLogoutRedirectUri: string;
  scope: string;
}
