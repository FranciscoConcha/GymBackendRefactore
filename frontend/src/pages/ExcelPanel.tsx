import { useState } from "react";
import { Download, FileSpreadsheet, Upload } from "lucide-react";
import {
  confirmUserImport,
  downloadExport,
  downloadTemplate,
  previewUserFile,
  type ImportPreview,
} from "../excel";
import type { DemoState } from "../domain";

export default function ExcelPanel({
  state,
  onImport,
}: {
  state: DemoState;
  onImport: (operation: (state: DemoState) => DemoState) => boolean;
}) {
  const [preview, setPreview] = useState<ImportPreview | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  async function perform(operation: () => Promise<void>) {
    setBusy(true);
    setError("");
    setSuccess("");
    try {
      await operation();
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : "No se pudo procesar el archivo.",
      );
    } finally {
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
        <dl className="export-counts">
          <div>
            <dt>Usuarios</dt>
            <dd>{state.members.length}</dd>
          </div>
          <div>
            <dt>Reservas</dt>
            <dd>{state.reservations.length}</dd>
          </div>
          <div>
            <dt>Visitas</dt>
            <dd>
              {state.reservations.reduce(
                (sum, r) => sum + (r.visits?.length ?? 0),
                0,
              )}
            </dd>
          </div>
        </dl>
        <button
          className="button"
          disabled={busy}
          onClick={() => void perform(() => downloadExport(state, "admin"))}
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
          Carga la plantilla con RUT, nombre, apellido, correo, categoría y
          rama. Se validará antes de crear usuarios.
        </p>
        <button
          className="text-button"
          disabled={busy}
          onClick={() => void perform(downloadTemplate)}
        >
          <FileSpreadsheet size={17} />
          Descargar plantilla
        </button>
        <label className="field">
          Archivo de usuarios (.xlsx)
          <input
            type="file"
            accept=".xlsx"
            disabled={busy}
            onChange={(event) => {
              const file = event.target.files?.[0];
              setPreview(null);
              if (file)
                void perform(async () => {
                  setPreview(await previewUserFile(file, state));
                });
            }}
          />
        </label>
        {preview && (
          <div className="import-preview">
            <h3>Vista previa</h3>
            <p>
              {preview.total} filas · {preview.members.length} válidas ·{" "}
              {preview.errors.length} con errores
            </p>
            {preview.errors.length > 0 && (
              <ul className="import-errors">
                {preview.errors.map((item) => (
                  <li key={item.row}>
                    Fila {item.row}: {item.message}
                  </li>
                ))}
              </ul>
            )}
            <ul className="import-names">
              {preview.members.slice(0, 10).map((member) => (
                <li key={member.id}>
                  {member.name} · {member.rut} ·{" "}
                  {member.branch ?? member.category}
                </li>
              ))}
            </ul>
            {preview.members.length > 10 && (
              <p className="muted">
                Y {preview.members.length - 10} usuarios más.
              </p>
            )}
            <button
              className="button"
              disabled={
                busy || !!preview.errors.length || !preview.members.length
              }
              onClick={() => {
                if (
                  onImport((current) =>
                    confirmUserImport(current, preview, "admin"),
                  )
                ) {
                  setPreview(null);
                  setSuccess("Usuarios creados con activación pendiente.");
                }
              }}
            >
              Confirmar importación ({preview.members.length})
            </button>
          </div>
        )}
      </section>
      {busy && (
        <p className="muted" role="status">
          Procesando archivo…
        </p>
      )}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      {success && <p role="status">{success}</p>}
    </div>
  );
}
