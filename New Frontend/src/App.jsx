import { Navigate, Route, Routes } from 'react-router-dom'
import AppLayout from './layouts/AppLayout.jsx'
import Dashboard from './pages/Dashboard.jsx'
import NewInspection from './pages/NewInspection.jsx'
import InspectionHistory from './pages/InspectionHistory.jsx'
import InspectionDetails from './pages/InspectionDetails.jsx'
import BrandManagement from './pages/BrandManagement.jsx'
import ModelManagement from './pages/ModelManagement.jsx'
import LabelProfileManagement from './pages/LabelProfileManagement.jsx'
import MasterLabelManagement from './pages/MasterLabelManagement.jsx'
import Login from './pages/Login.jsx'
import RequireAuth from './components/RequireAuth.jsx'
import RequireRole from './components/RequireRole.jsx'
import UserManagement from './pages/UserManagement.jsx'
import Settings from './pages/Settings.jsx'
import Welcome from './pages/Welcome.jsx'
import ForgotPassword from './pages/ForgotPassword.jsx'
import ResetPassword from './pages/ResetPassword.jsx'
import Profile from './pages/Profile.jsx'
import './App.css'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Welcome />} />
      <Route path="login" element={<Login />} />
      <Route path="forgot-password" element={<ForgotPassword />} />
      <Route path="reset-password" element={<ResetPassword />} />
      <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="inspections" element={<InspectionHistory />} />
        <Route path="inspections/:inspectionId" element={<InspectionDetails />} />
        <Route path="profile" element={<Profile />} />
        <Route element={<RequireRole roles={['Supervisor', 'Operator']} />}>
          <Route path="inspections/new" element={<NewInspection />} />
        </Route>
        <Route element={<RequireRole role="Supervisor" />}>
          <Route path="brands" element={<BrandManagement />} />
          <Route path="models" element={<ModelManagement />} />
          <Route path="label-profiles" element={<LabelProfileManagement />} />
          <Route path="master-labels" element={<MasterLabelManagement />} />
          <Route path="users" element={<UserManagement />} />
          <Route path="settings" element={<Settings />} />
        </Route>
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
