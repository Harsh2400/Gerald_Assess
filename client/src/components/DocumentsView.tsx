import { useEffect, useRef, useState } from 'react';
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
  const [uploading, setUploading] = useState(false);
  const [dragActive, setDragActive] = useState(false);
  const [uploadStatus, setUploadStatus] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

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

  async function uploadFiles(files: FileList | File[]) {
    const selected = Array.from(files);
    if (selected.length === 0 || uploading) return;

    setUploading(true);
    setError(null);
    let completed = 0;
    try {
      for (const file of selected) {
        setUploadStatus(`Uploading ${file.name} (${completed + 1} of ${selected.length})…`);
        await api.uploadDocument(file);
        completed += 1;
      }
      setUploadStatus(`${completed} ${completed === 1 ? 'document' : 'documents'} uploaded and indexed`);
      refresh();
    } catch (err) {
      setUploadStatus(null);
      setError(err instanceof Error ? err.message : 'Failed to upload document.');
      if (completed > 0) refresh();
    } finally {
      setUploading(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  return (
    <div className="manager-view">
      <div className="manager-view-inner">
        <div className="manager-header">
          <div>
            <span className="eyebrow">Knowledge base</span>
            <h2>Your documents</h2>
            <p>Add and manage the sources your assistant learns from.</p>
          </div>
          <button
            className="btn btn-primary"
            onClick={() => fileInputRef.current?.click()}
            disabled={uploading}
          >
            <span>＋</span> {uploading ? 'Uploading…' : 'Upload files'}
          </button>
        </div>

        <input
          ref={fileInputRef}
          className="visually-hidden"
          type="file"
          accept=".pdf,.docx,.md,.txt,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/markdown,text/plain"
          multiple
          onChange={(event) => event.target.files && uploadFiles(event.target.files)}
        />
        <button
          className={`upload-zone ${dragActive ? 'drag-active' : ''} ${uploading ? 'uploading' : ''}`}
          onClick={() => fileInputRef.current?.click()}
          onDragEnter={(event) => { event.preventDefault(); setDragActive(true); }}
          onDragOver={(event) => { event.preventDefault(); setDragActive(true); }}
          onDragLeave={(event) => { event.preventDefault(); setDragActive(false); }}
          onDrop={(event) => {
            event.preventDefault();
            setDragActive(false);
            uploadFiles(event.dataTransfer.files);
          }}
          disabled={uploading}
        >
          <span className="upload-icon">⇧</span>
          <strong>{uploading ? uploadStatus : 'Click to upload or drag and drop'}</strong>
          <small>PDF, DOCX, Markdown or TXT · maximum 10 MB each</small>
        </button>

        <div className="upload-helper">
          {uploadStatus && !uploading ? <span className="upload-success">✓ {uploadStatus}</span> : <span>Files are securely processed by your local RAG pipeline.</span>}
          <button onClick={() => setForm({ id: null, title: '', content: '' })}>Or paste content manually</button>
        </div>

        <div className="content-toolbar">
          <div><span>Home</span><b>›</b><strong>Knowledge base</strong></div>
          <span className="document-count">{documents.length} {documents.length === 1 ? 'document' : 'documents'}</span>
        </div>

        {error && <div className="error-banner">{error}</div>}

        {loading ? (
          <div className="empty-state">Loading...</div>
        ) : documents.length === 0 ? (
          <div className="empty-state">No documents yet. Add one to get started.</div>
        ) : (
          <div className="table-card documents-table-card"><table className="data-table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Status</th>
                <th>Chunks</th>
                <th>Updated</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {documents.map((d) => (
                <tr key={d.id} className="clickable" onClick={() => onViewChunks(d.id)}>
                  <td className="row-title"><span className="file-icon">▤</span><span>{d.title}<small>{d.sourceType.toUpperCase()}</small></span></td>
                  <td>
                    <span className="badge indexed"><i/> Indexed</span>
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
          </table></div>
        )}
      </div>

      {form && (
        <div className="modal-backdrop" onClick={() => setForm(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-heading"><span className="file-icon">▤</span><div><h2>{form.id ? 'Edit document' : 'Add document'}</h2><p>{form.id ? 'Changes will be re-indexed automatically.' : 'Create a new source for your assistant.'}</p></div></div>
            <div className="field">
              <label className="field-label">Title</label>
              <input
                className="text-input"
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                placeholder="e.g. Gerald RAG Product Guide"
              />
            </div>
            <div className="field">
              <label className="field-label">Content <span>Markdown supported</span></label>
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
