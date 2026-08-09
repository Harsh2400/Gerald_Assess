import { useEffect, useState } from 'react';
import { api } from '../api';
import type { ChunkSummary, DocumentSummary } from '../types';

export function ChunksView({
  documentId,
  onDocumentIdChange,
}: {
  documentId: string | null;
  onDocumentIdChange: (id: string | null) => void;
}) {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [chunks, setChunks] = useState<ChunkSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ id: string; text: string } | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.listDocuments().then(setDocuments).catch(() => {});
  }, []);

  const refresh = () => {
    setLoading(true);
    setError(null);
    api
      .listChunks(documentId ?? undefined)
      .then(setChunks)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load chunks.'))
      .finally(() => setLoading(false));
  };

  useEffect(refresh, [documentId]);

  async function handleDelete(id: string) {
    if (!confirm('Delete this chunk? It will no longer be retrievable.')) return;
    setError(null);
    try {
      await api.deleteChunk(id);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete chunk.');
    }
  }

  async function handleSaveEdit() {
    if (!editing || !editing.text.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await api.updateChunk(editing.id, editing.text);
      setEditing(null);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save chunk.');
    } finally {
      setSaving(false);
    }
  }

  const docTitleById = Object.fromEntries(documents.map((d) => [d.id, d.title]));

  return (
    <div className="manager-view">
      <div className="manager-view-inner">
        <div className="manager-header">
          <div>
            <span className="eyebrow">Knowledge base</span>
            <h2>Indexed chunks</h2>
            <p>Inspect and refine the searchable passages behind every answer.</p>
          </div>
        </div>

        <div className="content-toolbar chunk-toolbar">
          <div><span>Home</span><b>›</b><strong>Indexed chunks</strong></div>
          <select
            className="text-input"
            style={{ maxWidth: 320 }}
            value={documentId ?? ''}
            onChange={(e) => onDocumentIdChange(e.target.value || null)}
          >
            <option value="">All documents</option>
            {documents.map((d) => (
              <option key={d.id} value={d.id}>
                {d.title}
              </option>
            ))}
          </select>
        </div>

        {error && <div className="error-banner">{error}</div>}

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : chunks.length === 0 ? (
          <div className="empty-state">No chunks found.</div>
        ) : (
          <div className="table-card"><table className="data-table">
            <thead>
              <tr>
                <th>Document</th>
                <th>Heading</th>
                <th>Text</th>
                <th>Offsets</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {chunks.map((c) => (
                <tr key={c.id}>
                  <td className="row-title"><span className="file-icon chunk-icon">#</span><span>{docTitleById[c.documentId] ?? c.documentId}</span></td>
                  <td>{c.heading}</td>
                  <td className="chunk-text-preview">
                    {c.text.length > 160 ? c.text.slice(0, 160) + '...' : c.text}
                  </td>
                  <td>
                    {c.startChar >= 0 ? (
                      <span className="badge">
                        {c.startChar}–{c.endChar}
                      </span>
                    ) : (
                      <span className="badge stale-badge">edited</span>
                    )}
                  </td>
                  <td>
                    <div className="row-actions">
                      <button className="btn btn-sm" onClick={() => setEditing({ id: c.id, text: c.text })}>
                        Edit
                      </button>
                      <button className="btn btn-sm btn-danger" onClick={() => handleDelete(c.id)}>
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table></div>
        )}
      </div>

      {editing && (
        <div className="modal-backdrop" onClick={() => setEditing(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>Edit chunk</h2>
            <div className="field">
              <textarea
                className="text-input"
                rows={10}
                value={editing.text}
                onChange={(e) => setEditing({ ...editing, text: e.target.value })}
              />
            </div>
            <div className="modal-actions">
              <button className="btn" onClick={() => setEditing(null)}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={handleSaveEdit} disabled={saving}>
                {saving ? 'Saving...' : 'Save & re-embed'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
