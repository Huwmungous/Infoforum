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

// ── Git repository types ────────────────────────────────────────

export enum GitCredentialType {
  PersonalAccessToken = 0,
  SshKey = 1,
}

export enum ProjectType {
  Unknown = 0,
  DotNet = 1,
  ReactTypeScript = 2,
  AngularTypeScript = 3,
  Delphi = 4,
}

export interface GitRepository {
  id: string;
  name: string;
  url: string;
  defaultBranch: string;
  detectedProjectType: ProjectType;
  clonedAt: string;
  lastPulledAt: string;
}

export interface ConversationRepo {
  repositoryId: string;
  name: string;
  url: string;
  detectedProjectType: ProjectType;
  branchName: string;
  enabled: boolean;
  createdAt: string;
}

export interface BuildResult {
  success: boolean;
  exitCode: number;
  output: string;
  errors: string;
  projectType: ProjectType;
  buildCommand: string;
  diagnostics: BuildDiagnostic[];
}

export interface BuildDiagnostic {
  severity: string;
  code: string;
  message: string;
  file: string;
  line: number;
  column: number;
}
