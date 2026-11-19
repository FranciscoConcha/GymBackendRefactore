import { useRef, useState } from "react";
import { Download, Upload } from "lucide-react";
import { api, type ImportPreview, type ImportResult } from "../api";
import { ReportFilterFields } from "./Records";
export default function ExcelPanel() {
  const [preview, setPreview] = useState<ImportPreview>();
  const [result, setResult] = useState<ImportResult>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [filters, setFilters] = useState<import("../api").ReportFilters>({});
  const key = useRef<string | null>(null);
  const sending = useRef(false);
  async function perform(operation: () => Promise<void>) {
    if (sending.current) return;
    sending.current = true;
    setBusy(true);
    setError("");
    try {
      await operation();
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo procesar el archivo.",
      );
    } finally {
      sending.current = false;
      setBusy(false);
    }
  }
  return (
    <div className="excel-layout">
      <section className="card">
        <div className="section-title">
          <h2>Exportar información</h2>
          <Download size={21} />
        </div>
        <p className="muted">
          Usuarios, sesiones, reservas, visitas, grupos e inasistencias en un
          solo Excel.
        </p>
        <ReportFilterFields value={filters} onChange={setFilters} />
        <button
          className="button"
          disabled={busy}
          onClick={() =>
            void perform(async () => {
              const { blob, filename } = await api.export(filters);
              const url = URL.createObjectURL(blob);
              const link = document.createElement("a");
              link.href = url;
              link.download = filename;
              link.click();
              setTimeout(() => URL.revokeObjectURL(url), 1000);
            })
          }
        >
          <Download size={18} />
          Exportar todo (.xlsx)
        </button>
      </section>
      <section className="card">
        <div className="section-title">
          <h2>Importar usuarios</h2>
          <Upload size={21} />
        </div>
        <p className="muted">
          Carga un archivo con RUT, Nombre, Apellido, Correo, Categoria y Rama.
          Se validará antes de crear usuarios.
        </p>
        <label className="field">
          Archivo de usuarios (.xlsx)
          <input
            type="file"
            accept=".xlsx"
            disabled={busy}
            onChange={(e) => {
              const file = e.target.files?.[0];
              setPreview(undefined);
              setResult(undefined);
              key.current = null;
              if (file)
                void perform(async () =>
                  setPreview(await api.previewImport(file)),
                );
            }}
          />
        </label>
        {preview && (
          <div className="import-preview">
            <h3>Vista previa</h3>
            <p>
              {preview.summary.create} para crear · {preview.summary.skip}{" "}
              omitidas · {preview.summary.error} con errores
            </p>
            <p>
              Vigencia: {new Date(preview.expiresAt).toLocaleString("es-CL")}
            </p>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Fila</th>
                    <th>Usuario</th>
                    <th>RUT</th>
                    <th>Acción</th>
                    <th>Mensajes</th>
                  </tr>
                </thead>
                <tbody>
                  {preview.rows.map((r) => (
                    <tr key={r.row}>
                      <td>{r.row}</td>
                      <td>
                        {r.firstName} {r.lastName}
                      </td>
                      <td>{r.rut}</td>
                      <td>{r.action}</td>
                      <td>{r.messages.join(" · ")}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <button
              className="button"
              disabled={busy || !!result || !preview.summary.create}
              onClick={() =>
                void perform(async () => {
                  key.current ??= crypto.randomUUID();
                  setResult(await api.confirmImport(preview.id, key.current));
                })
              }
            >
              Confirmar importación ({preview.summary.create})
            </button>
          </div>
        )}
        {result && (
          <div role="status">
            <p>
              {result.created} creados · {result.skipped} omitidos ·{" "}
              {result.failed} fallidos
            </p>
            <ul>
              {result.rows.map((r) => (
                <li key={r.row}>
                  Fila {r.row}: {r.action} · {r.messages.join(" · ")}
                </li>
              ))}
            </ul>
          </div>
        )}
      </section>
      {busy && <p role="status">Procesando archivo…</p>}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}
