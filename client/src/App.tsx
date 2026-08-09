import { useState } from 'react';
import { ChatView } from './components/ChatView';
import { DocumentsView } from './components/DocumentsView';
import { ChunksView } from './components/ChunksView';

type WorkspaceView = 'documents' | 'chunks';

function Icon({ name, size = 18 }: { name: string; size?: number }) {
  const paths: Record<string, React.ReactNode> = {
    home: <><path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10v10h13V10M9 20v-6h6v6"/></>,
    file: <><path d="M6 2.5h8l4 4V21H6z"/><path d="M14 2.5v4h4M9 11h6M9 15h6"/></>,
    chunks: <><rect x="4" y="4" width="7" height="7" rx="1"/><rect x="13" y="4" width="7" height="7" rx="1"/><rect x="4" y="13" width="7" height="7" rx="1"/><rect x="13" y="13" width="7" height="7" rx="1"/></>,
    team: <><circle cx="9" cy="8" r="3"/><path d="M3.5 20v-2a5.5 5.5 0 0 1 11 0v2M16 5.5a3 3 0 0 1 0 5.8M17 14a5 5 0 0 1 3.5 4.8V20"/></>,
    settings: <><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2h-4V21a1.7 1.7 0 0 0-1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1L4.2 17l.1-.1a1.7 1.7 0 0 0 .3-1.9A1.7 1.7 0 0 0 3 14H2.8v-4H3a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.3-1.9L4.2 7 7 4.2l.1.1A1.7 1.7 0 0 0 9 4.6a1.7 1.7 0 0 0 1-1.6v-.2h4V3a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.9-.3l.1-.1L19.8 7l-.1.1a1.7 1.7 0 0 0-.3 1.9 1.7 1.7 0 0 0 1.6 1h.2v4H21a1.7 1.7 0 0 0-1.6 1Z"/></>,
  };
  return <svg className="icon" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{paths[name]}</svg>;
}

function App() {
  const [view, setView] = useState<WorkspaceView>('documents');
  const [chunksFilterDocId, setChunksFilterDocId] = useState<string | null>(null);
  const [mobilePane, setMobilePane] = useState<'workspace' | 'assistant'>('workspace');

  const showDocuments = () => { setView('documents'); setMobilePane('workspace'); };
  const showChunks = (id: string | null = null) => { setChunksFilterDocId(id); setView('chunks'); setMobilePane('workspace'); };

  return (
    <div className="page-frame">
      <div className="app-shell">
        <aside className="main-sidebar">
          <div className="traffic-lights"><i/><i/><i/></div>
          <div className="profile">
            <div className="profile-avatar">A</div>
            <div><strong>Gerald RAG</strong><span>Knowledge workspace</span></div>
            <span className="chevron">⌄</span>
          </div>

          <div className="nav-label">Workspace</div>
          <nav className="side-nav">
            <button className={view === 'documents' ? 'active' : ''} onClick={showDocuments}><Icon name="home"/>Overview</button>
            <button onClick={() => showChunks()} className={view === 'chunks' && !chunksFilterDocId ? 'active' : ''}><Icon name="chunks"/>All chunks</button>
          </nav>

          <div className="nav-label projects-label">Knowledge base</div>
          <nav className="side-nav project-nav">
            <button className={view === 'documents' ? 'selected-subtle' : ''} onClick={showDocuments}><Icon name="file"/>Documents</button>
            <button className={view === 'chunks' ? 'selected-subtle' : ''} onClick={() => showChunks()}><Icon name="chunks"/>Indexed chunks</button>
          </nav>

          <div className="sidebar-spacer"/>
          <button className="settings-button"><Icon name="settings"/>Settings</button>
        </aside>

        <main className={`workspace-pane ${mobilePane === 'workspace' ? 'mobile-active' : ''}`}>
          {view === 'documents' ? (
            <DocumentsView onViewChunks={(id) => showChunks(id)} />
          ) : (
            <ChunksView documentId={chunksFilterDocId} onDocumentIdChange={setChunksFilterDocId} />
          )}
        </main>

        <section className={`assistant-pane ${mobilePane === 'assistant' ? 'mobile-active' : ''}`}>
          <ChatView />
        </section>

        <nav className="mobile-nav" aria-label="Mobile navigation">
          <button className={mobilePane === 'workspace' ? 'active' : ''} onClick={() => setMobilePane('workspace')}><Icon name="file"/>Knowledge</button>
          <button className={mobilePane === 'assistant' ? 'active' : ''} onClick={() => setMobilePane('assistant')}><span className="spark-icon">✦</span>Assistant</button>
        </nav>
      </div>
    </div>
  );
}

export default App;
