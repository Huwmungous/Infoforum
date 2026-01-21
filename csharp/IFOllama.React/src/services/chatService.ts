import * as signalR from '@microsoft/signalr';

const API_BASE_URL = import.meta.env.VITE_API_URL || '';

export class ChatService {
  private connection: signalR.HubConnection | null = null;
  private accessToken: string | null = null;

  async connect(accessToken: string): Promise<void> {
    this.accessToken = accessToken;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/chat`, {
        accessTokenFactory: () => this.accessToken || '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.connection.onreconnecting((error) => {
      console.log('SignalR reconnecting...', error);
    });

    this.connection.onreconnected((connectionId) => {
      console.log('SignalR reconnected:', connectionId);
    });

    this.connection.onclose((error) => {
      console.log('SignalR connection closed:', error);
    });

    await this.connection.start();
    console.log('SignalR connected');
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async streamChat(
    modelName: string,
    history: string[],
    conversationId: string,
    userId: string,
    enabledTools: string[] | null,
    onToken: (token: string) => void,
    onComplete: () => void,
    onError: (error: string) => void
  ): Promise<void> {
    if (!this.connection) {
      onError('Not connected to chat service');
      return;
    }

    try {
      const stream = this.connection.stream<string>(
        'StreamChat',
        modelName,
        history,
        conversationId,
        userId,
        enabledTools
      );

      for await (const token of stream) {
        onToken(token);
      }

      onComplete();
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Unknown error';
      onError(message);
    }
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

// Singleton instance
export const chatService = new ChatService();
