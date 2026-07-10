import { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { useAuth } from 'react-oidc-context';
import { useReleaseStore } from './store/useReleaseStore';
import { Layout } from './components/Layout';
import { Releases } from './components/Releases';
import { ReleaseAnalyticsView } from './components/ReleaseAnalyticsView';
import { setAccessToken } from './api';

export default function App() {
  const auth = useAuth();
  const { fetchData, isLoading, error } = useReleaseStore();

  useEffect(() => {
    setAccessToken(auth.user?.access_token);
  }, [auth.user]);

  useEffect(() => {
    const isCallback = window.location.pathname === '/callback';
    if (!auth.isLoading && !auth.isAuthenticated && !isCallback) {
      auth.signinRedirect();
    }
  }, [auth.isLoading, auth.isAuthenticated]);

  useEffect(() => {
    if (auth.isAuthenticated) {
      fetchData();
    }
  }, [auth.isAuthenticated]);

  const handleLogout = async () => {
    await auth.signoutRedirect();
  };

  if (auth.isLoading || !auth.isAuthenticated) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-text-secondary font-mono text-xs uppercase tracking-widest">Loading...</p>
        </div>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary mx-auto"></div>
          <p className="mt-4 text-text-secondary font-mono text-xs uppercase tracking-widest">Loading data...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background p-6">
        <div className="text-center max-w-md border border-border p-12 bg-surface rounded-sm">
          <h2 className="text-xs font-mono uppercase tracking-[0.2em] text-text-secondary mb-4">Connection error</h2>
          <p className="text-sm text-text-primary mb-8 leading-relaxed font-mono">{error}</p>
          <button
            onClick={() => fetchData()}
            className="px-8 py-3 bg-primary text-white text-xs font-bold uppercase tracking-widest hover:bg-primary/90 transition-all rounded-sm"
          >
            Retry
          </button>
        </div>
      </div>
    );
  }

  return (
    <BrowserRouter>
      <Layout onLogout={handleLogout}>
        <Routes>
          <Route path="/" element={<Releases />} />
          <Route path="/releases/:id/analytics" element={<ReleaseAnalyticsView />} />
          <Route path="/callback" element={null} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Layout>
    </BrowserRouter>
  );
}
