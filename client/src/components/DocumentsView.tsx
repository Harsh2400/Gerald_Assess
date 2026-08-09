import { useEffect, useState } from 'react';
import { api } from '../api';
import type { DocumentSummary } from '../types';

interface DocumentFormState {
  id: string | null; // null = creating
  title: string;
  content: string;
}

export function DocumentsView({ onViewChunks }: { onViewChunks: (documentId: string) => void }) {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<DocumentFormState | null>(null);
  const [saving, setSaving] = useState(false);

  const refresh = () => {
    setLoading(true);
    api
      .listDocuments()
      .then(setDocuments)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load documents.'))
      .finally(() => setLoading(false));
  };

  useEffect(refresh, []);

  async function openEdit(id: string) {
    setError(null);
    try {
      const doc = await api.getDocument(id);
      setForm({ id: doc.id, title: doc.title, content: doc.content });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load document.');
    }
  }

  async function handleDelete(id: string, title: string) {
    if (!confirm(`Delete "${title}" and all its chunks? This can't be undone.`)) return;
    setError(null);
    try {
      await api.deleteDocument(id);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete document.');
    }
  }

  async function handleSave() {
    if (!form || !form.title.trim() || !form.content.trim()) return;
    setSaving(true);
    setError(null);
    try {
      if (form.id) {
        await api.updateDocument(form.id, form.title.trim(), form.content);
      } else {
        await api.createDocument(form.title.trim(), form.content);
      }
      setForm(null);
      refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to save document.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="manager-view">
      <div className="manager-view-inner">
        <div className="manager-header">
          <div>
            <h2>Documents</h2>
            <p>Source documents in the knowledge base. Editing re-chunks and re-embeds automatically.</p>
          </div>
          <button
            className="btn btn-primary"
            onClick={() => setForm({ id: null, title: '', content: '' })}
          >
            + Add document
          </button>
        </div>

        {error && <div className="error-banner">{error}</div>}

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : documents.length === 0 ? (
          <div className="empty-state">No documents yet. Add one to get started.</div>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Source</th>
                <th>Chunks</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => (
                <tr key={d.id} className="clickable" onClick={() => onViewChunks(d.id)}>
                  <td className="row-title">{d.title}</td>
                  <td>
                    <span className="badge">{d.sourceType}</span>
                  </td>
                  <td>{d.chunkCount}</td>
                  <td className="row-meta">{new Date(d.updatedAt).toLocaleString()}</td>
                  <td>
                    <div className="row-actions" onClick={(e) => e.stopPropagation()}>
                      <button className="btn btn-sm" onClick={() => openEdit(d.id)}>
                        Edit
                      </button>
                      <button className="btn btn-sm btn-danger" onClick={() => handleDelete(d.id, d.title)}>
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {form && (
        <div className="modal-backdrop" onClick={() => setForm(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h2>{form.id ? 'Edit document' : 'Add document'}</h2>
            <div className="field">
              <label className="field-label">Title</label>
              <input
                className="text-input"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                placeholder="e.g. Aurora Cloud Storage - Mobile App"
              />
            </div>
            <div className="field">
              <label className="field-label">Content (Markdown - use ## headings to control chunk boundaries)</label>
              <textarea
                className="text-input"
                rows={12}
                value={form.content}
                onChange={(e) => setForm({ ...form, content: e.target.value })}
                placeholder={'# Title\n\n## Section Heading\nBody text...'}
              />
            </div>
            <div className="modal-actions">
              <button className="btn" onClick={() => setForm(null)}>
                Cancel
              </button>
              <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Saving...' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
