import { Navigate, Route, Routes } from 'react-router-dom'
import AppLayout from './layouts/AppLayout.jsx'
import Dashboard from './pages/Dashboard.jsx'
import NewInspection from './pages/NewInspection.jsx'
import InspectionHistory from './pages/InspectionHistory.jsx'
import InspectionDetails from './pages/InspectionDetails.jsx'
import BrandManagement from './pages/BrandManagement.jsx'
import ModelManagement from './pages/ModelManagement.jsx'
import LabelProfileManagement from './pages/LabelProfileManagement.jsx'
import Login from './pages/Login.jsx'
import RequireAuth from './components/RequireAuth.jsx'
import RequireRole from './components/RequireRole.jsx'
import UserManagement from './pages/UserManagement.jsx'
import Settings from './pages/Settings.jsx'
import './App.css'

function App() {
  return (
    <Routes>
      <Route path="login" element={<Login />} />
      <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
        <Route index element={<Dashboard />} />
        <Route path="inspections/new" element={<NewInspection />} />
        <Route path="inspections" element={<InspectionHistory />} />
        <Route path="inspections/:inspectionId" element={<InspectionDetails />} />
        <Route path="brands" element={<BrandManagement />} />
        <Route path="models" element={<ModelManagement />} />
        <Route path="label-profiles" element={<LabelProfileManagement />} />
        <Route element={<RequireRole role="Admin" />}>
          <Route path="users" element={<UserManagement />} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
