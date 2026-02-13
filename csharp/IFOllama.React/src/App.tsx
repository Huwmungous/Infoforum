import { useState, useCallback } from 'react';
import { Chat } from './components/Chat';
import { ConversationDrawer } from './components/ConversationDrawer';

function App() {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [selectedConversationId, setSelectedConversationId] = useState<string | null>(null);
  const [conversationTitle, setConversationTitle] = useState<string | null>(null);
  const [chatKey, setChatKey] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);

  const handleSelectConversation = useCallback((id: string) => {
    setSelectedConversationId(id);
    setChatKey(prev => prev + 1);
    // Close drawer on narrow screens
    if (window.innerWidth < 900) {
      setDrawerOpen(false);
    }
  }, []);

  const handleNewChat = useCallback(() => {
    setSelectedConversationId(null);
    setChatKey(prev => prev + 1);
    if (window.innerWidth < 900) {
      setDrawerOpen(false);
    }
  }, []);

  const handleConversationCreated = useCallback((id: string) => {
    setSelectedConversationId(id);
    setRefreshKey(prev => prev + 1);
  }, []);

  const handleTitleGenerated = useCallback(() => {
    setRefreshKey(prev => prev + 1);
  }, []);

  return (
    <div className={`if-app-container app-layout ${drawerOpen ? 'drawer-open' : ''}`}>
      <ConversationDrawer
        isOpen={drawerOpen}
        onToggle={() => setDrawerOpen(prev => !prev)}
        onSelectConversation={handleSelectConversation}
        selectedId={selectedConversationId}
        onNewChat={handleNewChat}
        refreshKey={refreshKey}
        onTitleChange={setConversationTitle}
      />
      <Chat
        key={chatKey}
        initialConversationId={selectedConversationId ?? undefined}
        conversationTitle={conversationTitle}
        onConversationCreated={handleConversationCreated}
        onTitleGenerated={handleTitleGenerated}
      />
    </div>
  );
}

export default App;
