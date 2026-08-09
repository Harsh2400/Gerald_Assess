import { useState } from 'react';
import { ChatView } from './components/ChatView';
import { DocumentsView } from './components/DocumentsView';
import { ChunksView } from './components/ChunksView';

type Tab = 'chat' | 'documents' | 'chunks';

function App() {
  const [tab, setTab] = useState<Tab>('chat');
  const [chunksFilterDocId, setChunksFilterDocId] = useState<string | null>(null);

  return (
    <>
      <header className="app-header">
        <div className="app-brand">
          <h1>Aurora Knowledge Assistant</h1>
          <span>hybrid RAG · BM25 + semantic + rerank</span>
        </div>
        <nav className="tab-nav">
          <button className={tab === 'chat' ? 'active' : ''} onClick={() => setTab('chat')}>
            Chat
          </button>
          <button className={tab === 'documents' ? 'active' : ''} onClick={() => setTab('documents')}>
            Documents
          </button>
          <button className={tab === 'chunks' ? 'active' : ''} onClick={() => setTab('chunks')}>
            Chunks
          </button>
        </nav>
      </header>

      <div className="app-body">
        {tab === 'chat' && <ChatView />}
        {tab === 'documents' && (
          <DocumentsView
            onViewChunks={(docId) => {
              setChunksFilterDocId(docId);
              setTab('chunks');
            }}
          />
        )}
        {tab === 'chunks' && (
          <ChunksView documentId={chunksFilterDocId} onDocumentIdChange={setChunksFilterDocId} />
        )}
      </div>
    </>
  );
}

export default App;
