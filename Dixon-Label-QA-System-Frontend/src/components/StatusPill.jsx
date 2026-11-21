import { getStatusClass, getStatusLabel } from '../utils/statusLabel.js'

function StatusPill({ status, label }) {
  const className = getStatusClass(status)
  const displayLabel = label ?? getStatusLabel(status)

  return <span className={`status-pill ${className}`}>{displayLabel}</span>
}

export default StatusPill
