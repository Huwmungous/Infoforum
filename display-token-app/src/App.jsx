import React, { useState, useEffect } from 'react';
import TokenDisplay from './Tokendisplay';
import PatientLogin from './PatientLogin';
import PatientTokenDisplay from './PatientTokenDisplay';
import { patientAuthService } from './PatientAuthService';

/**
 * Tab Button Component
 */
const TabButton = ({ active, onClick, children }) => (
  <button
    onClick={onClick}
    className={`px-6 py-3 font-medium text-sm transition-colors ${
      active
        ? 'bg-white text-blue-600 border-b-2 border-blue-600'
        : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
    }`}
  >
    {children}
  </button>
);



/**
 * Patient Login Tab - Uses direct ROPC flow
 */
const PatientLoginTab = () => {
  const [patientUser, setPatientUser] = useState(null);
  const [loading, setLoading] = useState(true);

  // Check for existing patient session on mount
  useEffect(() => {
    const existingUser = patientAuthService.getUser();
    if (existingUser) {
      // Verify token hasn't expired
      const accessTokenDecoded = patientAuthService.decodeJWT(existingUser.access_token);
      if (accessTokenDecoded?.exp && accessTokenDecoded.exp * 1000 > Date.now()) {
        setPatientUser(existingUser);
      } else {
        // Token expired, clear it
        patientAuthService.logout();
      }
    }
    setLoading(false);
  }, []);

  const handleLoginSuccess = (user) => {
    setPatientUser(user);
  };

  const handleLogout = () => {
    setPatientUser(null);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gray-100 flex items-center justify-center">
        <div className="text-gray-600">Loading...</div>
      </div>
    );
  }

  if (patientUser) {
    return <PatientTokenDisplay user={patientUser} onLogout={handleLogout} />;
  }

  return <PatientLogin onLoginSuccess={handleLoginSuccess} />;
};

function App() {
  // Determine initial tab from URL hash or default to 'staff'
  const getInitialTab = () => {
    const hash = window.location.hash.replace('#', '');
    return hash === 'patient' ? 'patient' : 'staff';
  };
  
  const [activeTab, setActiveTab] = useState(getInitialTab);

  // Update URL hash when tab changes
  const handleTabChange = (tab) => {
    setActiveTab(tab);
    window.location.hash = tab;
  };

  // Listen for hash changes (browser back/forward)
  useEffect(() => {
    const handleHashChange = () => {
      const hash = window.location.hash.replace('#', '');
      setActiveTab(hash === 'patient' ? 'patient' : 'staff');
    };
    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Tab Navigation */}
      <div className="bg-white shadow-sm">
        <div className="max-w-4xl mx-auto">
          <div className="flex">
            <TabButton
              active={activeTab === 'staff'}
              onClick={() => handleTabChange('staff')}
            >
              Staff Login
            </TabButton>
            <TabButton
              active={activeTab === 'patient'}
              onClick={() => handleTabChange('patient')}
            >
              Patient Login
            </TabButton>
          </div>
        </div>
      </div>

      {/* Tab Content */}
      {activeTab === 'staff' ? (
        <TokenDisplay />
      ) : (
        <PatientLoginTab />
      )}
    </div>
  );
}

export default App;
