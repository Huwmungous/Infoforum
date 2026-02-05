import * as signalR from '@microsoft/signalr';

const API_BASE_URL = import.meta.env.VITE_API_URL || '';

export class ChatService {
  private connection: signalR.HubConnection | null = null;
  private accessToken: string | null = null;

  async connect(accessToken: string): Promise<void> {
    // Stop any existing connection first
    if (this.connection) {
      const old = this.connection;
      this.connection = null;
      await old.stop();
    }

    this.accessToken = accessToken;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/chathub`, {
        accessTokenFactory: () => this.accessToken || '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    connection.onreconnecting((error) => {
      console.log('SignalR reconnecting...', error);
    });

    connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
    });

    connection.onclose((error) => {
      console.log('SignalR connection closed:', error);
    });

    await connection.start();
    this.connection = connection;
    console.log('SignalR connected');
  }

  async disconnect(): Promise<void> {
    const conn = this.connection;
    this.connection = null;
    if (conn) {
      await conn.stop();
    }
  }

  streamChat(
    modelName: string,
    history: string[],
    conversationId: string,
    userId: string,
    enabledTools: string[] | null,
    onToken: (token: string) => void,
    onComplete: () => void,
    onError: (error: string) => void
  ): signalR.ISubscription<string> | null {
    if (!this.connection) {
      onError('Not connected to chat service');
      return null;
    }

    const stream = this.connection.stream<string>(
      'StreamChat',
      modelName,
      history,
      conversationId,
      userId,
      enabledTools
    );

    const subscription = stream.subscribe({
      next: (token) => {
        onToken(token);
      },
      complete: () => {
        onComplete();
      },
      error: (error) => {
        const message = error instanceof Error ? error.message : String(error);
        onError(message);
      },
    });

    return subscription;
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

// Singleton instance
export const chatService = new ChatService();
