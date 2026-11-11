import { FileSearch } from 'lucide-react'

function EmptyState({ title, description, icon: Icon = FileSearch, children }) {
  return (
    <div className="empty-state">
      <div>
        <div className="empty-state-icon"><Icon size={19} strokeWidth={1.7} /></div>
        <h2 className="empty-state-title">{title}</h2>
        <p className="empty-state-description">{description}</p>
        {children}
      </div>
    </div>
  )
}

export default EmptyState
