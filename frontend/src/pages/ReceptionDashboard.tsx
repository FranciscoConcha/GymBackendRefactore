import { ArrowRight, ScanLine, Users } from "lucide-react";
import {
  formatRut,
  presentReservations,
  ROOM_CAPACITY,
  type DemoState,
} from "../domain";

export default function ReceptionDashboard({
  state,
  onScan,
}: {
  state: DemoState;
  onScan: () => void;
}) {
  const present = presentReservations(state);
  return (
    <div className="reception-dashboard">
      <section className="reception-status card">
        <span className="option-icon">
          <Users size={25} />
        </span>
        <div>
          <span className="eyebrow">AFORO ACTUAL</span>
          <strong>
            {present.length}
            <small> / {ROOM_CAPACITY}</small>
          </strong>
          <p>
            {Math.max(0, ROOM_CAPACITY - present.length)} lugares disponibles
          </p>
        </div>
        <button className="button" onClick={onScan}>
          <ScanLine size={18} />
          Escanear QR
          <ArrowRight size={18} />
        </button>
      </section>
      <section className="card">
        <div className="section-title">
          <h2>Personas dentro</h2>
          <span className="live-label">
            <span />
            Ahora
          </span>
        </div>
        {present.length ? (
          present.map((reservation) => {
            const member = state.members.find(
              (value) => value.id === reservation.memberId,
            );
            return (
              <div className="present-member" key={reservation.id}>
                <span className="avatar">{member?.name.slice(0, 1)}</span>
                <div>
                  <strong>{member?.name}</strong>
                  <span>
                    {formatRut(member?.rut ?? "")} ·{" "}
                    {member?.branch ?? member?.category}
                  </span>
                  <span>
                    Sesión {reservation.slot.start} a {reservation.slot.end}
                  </span>
                </div>
              </div>
            );
          })
        ) : (
          <div className="panel-empty">
            <Users size={28} />
            <h3>La sala está libre</h3>
          </div>
        )}
      </section>
    </div>
  );
}
