export function getStatusLabel(status) {
  return String(status ?? 'unavailable').toLowerCase().replaceAll('_', ' ')
}

export function getStatusClass(status) {
  const classNames = {
    connected: 'status-success',
    ready: 'status-success',
    warning: 'status-warning',
    unavailable: 'status-muted',
    error: 'status-danger',
    pass: 'status-success',
    fail: 'status-danger',
    review_required: 'status-warning',
    processing: 'status-muted',
    blocked: 'status-danger',
    not_applicable: 'status-muted',
    not_started: 'status-muted',
    ready_for_processing: 'status-success',
    cancelled: 'status-muted',
  }

  return classNames[String(status ?? 'unavailable').toLowerCase()] ?? 'status-muted'
}
