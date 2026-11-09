import { Route, Routes } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { LoginPage } from './features/auth/LoginPage';
import { HomeRedirect, RequireAuth, RequireRole } from './features/auth/RequireAuth';
import { EmployeeFormPage } from './features/employees/EmployeeFormPage';
import { EmployeesListPage } from './features/employees/EmployeesListPage';
import { PayslipPage } from './features/payroll/PayslipPage';

/** Routes only; providers (router, auth) are wired in main.tsx so tests can supply their own. */
export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<AppLayout />}>
          <Route element={<RequireRole role="HR" />}>
            <Route path="/employees" element={<EmployeesListPage />} />
            <Route path="/employees/new" element={<EmployeeFormPage />} />
            <Route path="/employees/:id/edit" element={<EmployeeFormPage />} />
            <Route path="/employees/:id/payslip" element={<PayslipPage />} />
          </Route>
          <Route element={<RequireRole role="Employee" />}>
            <Route path="/me/payslip" element={<PayslipPage />} />
          </Route>
        </Route>
      </Route>
      <Route path="*" element={<HomeRedirect />} />
    </Routes>
  );
}
