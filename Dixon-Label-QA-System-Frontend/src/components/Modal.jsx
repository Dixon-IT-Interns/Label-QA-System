import { X } from 'lucide-react'

function Modal({ title, onClose, children, wide = false }) {
  return (
    <div className="modal-backdrop" onMouseDown={onClose} role="presentation">
      <section
        aria-labelledby="modal-title"
        aria-modal="true"
        className={`modal${wide ? ' modal-wide' : ''}`}
        onMouseDown={(event) => event.stopPropagation()}
        role="dialog"
      >
        <header className="modal-header">
          <h2 className="panel-title" id="modal-title">{title}</h2>
          <button aria-label="Close dialog" className="icon-button" onClick={onClose} type="button">
            <X size={16} />
          </button>
        </header>
        {children}
      </section>
    </div>
  )
}

export default Modal
