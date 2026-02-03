import React, { useState, useEffect, useMemo } from 'react';
import { useAppContext, useAuth } from '@if/web-common-react';

// Logo reference
const logo = new URL('/IF-Logo.png', import.meta.url).href;

/**
 * DownloadList component
 * Displays available software downloads with authentication
 */
const DownloadList = () => {
  const { createLogger, config } = useAppContext();
  const { user, signout } = useAuth();
  const [downloads, setDownloads] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [downloading, setDownloading] = useState(null);

  // Create logger for this component
  const logger = useMemo(() => createLogger('DownloadList'), [createLogger]);

  // Fetch available downloads
  useEffect(() => {
    const fetchDownloads = async () => {
      try {
        setLoading(true);
        setError(null);

        // fetch() is intercepted by setupFetchInterceptor which adds Bearer token automatically
        const response = await fetch('/api/downloads');

        if (!response.ok) {
          throw new Error(`Failed to fetch downloads: ${response.statusText}`);
        }

        const data = await response.json();
        setDownloads(data);

        logger.info(`Loaded ${data.length} downloads for user ${user?.profile?.preferred_username}`);
      } catch (err) {
        console.error('Error fetching downloads:', err);
        setError(err.message);
        logger.error('Failed to fetch downloads', err);
      } finally {
        setLoading(false);
      }
    };

    if (user) {
      fetchDownloads();
    }
  }, [user, logger]);

  const handleDownload = async (download) => {
    try {
      setDownloading(download.filename);

      logger.info(`Download initiated: ${JSON.stringify({
        filename: download.filename,
        version: download.version,
        userId: user?.profile?.sub,
        username: user?.profile?.preferred_username,
      })}`);

      // Use fetch (intercepted - Bearer token added automatically) to download the file
      const response = await fetch(`/api/downloads/${download.filename}`);

      if (!response.ok) {
        throw new Error(`Download failed: ${response.statusText}`);
      }

      // Get the file as a blob and trigger browser download
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = download.filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);

      logger.info(`Download completed: ${download.filename}`);
    } catch (err) {
      console.error('Download error:', err);
      logger.error(`Download failed: ${download.filename}`, err);
      setError(`Failed to download ${download.filename}: ${err.message}`);
    } finally {
      setDownloading(null);
    }
  };

  const formatFileSize = (bytes) => {
    if (!bytes) return 'Unknown';
    const sizes = ['B', 'KB', 'MB', 'GB'];
    let order = 0;
    let size = bytes;

    while (size >= 1024 && order < sizes.length - 1) {
      order++;
      size /= 1024;
    }

    return `${size.toFixed(2)} ${sizes[order]}`;
  };

  const DownloadIcon = () => (
    <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} 
        d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
    </svg>
  );

  const SpinnerIcon = () => (
    <svg className="w-6 h-6 animate-spin" fill="none" viewBox="0 0 24 24">
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
  );

  return (
    <div className="min-h-screen bg-if-window flex flex-col">
      {/* Header */}
      <header className="bg-[#333] text-if-window px-6 py-3 flex justify-between items-center">
        <div className="flex items-center gap-3">
          <img src={logo} alt="IF" className="w-12 h-12" />
          <h1 className="text-xl font-medium">Software Downloads</h1>
          <span className="text-sm opacity-70">v1.0</span>
        </div>
        <div className="flex items-center gap-4">
          <span className="text-sm opacity-70">
            {user?.profile?.preferred_username || user?.profile?.email}
          </span>
          <button
            onClick={() => signout()}
            className="px-4 py-2 bg-if-hl-medium text-white rounded shadow-if-lg border border-if-dark hover:bg-if-hl-dark transition-colors"
          >
            Sign Out
          </button>
        </div>
      </header>

      <div className="max-w-4xl mx-auto p-6 flex-1">
        {/* Welcome Card */}
        <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 mb-6">
          <h2 className="text-xl font-medium text-if-dark mb-2">Welcome, {user?.profile?.given_name || user?.profile?.preferred_username}!</h2>
          <p className="text-if-medium">
            Download the latest versions of Infoforum software below.
          </p>
        </div>

        {/* Loading State */}
        {loading && (
          <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-8 text-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-if-hl-medium mx-auto mb-4"></div>
            <p className="text-if-medium">Loading available downloads...</p>
          </div>
        )}

        {/* Error State */}
        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-6 mb-6">
            <h3 className="text-red-800 font-medium mb-2">Error Loading Downloads</h3>
            <p className="text-red-600">{error}</p>
            <button 
              onClick={() => { setError(null); window.location.reload(); }}
              className="mt-4 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700 transition-colors"
            >
              Retry
            </button>
          </div>
        )}

        {/* Downloads List */}
        {!loading && !error && downloads.length === 0 && (
          <div className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-8 text-center">
            <p className="text-if-medium">No downloads available at this time.</p>
          </div>
        )}

        {!loading && !error && downloads.length > 0 && (
          <div className="space-y-4">
            {downloads.map((download, index) => (
              <div 
                key={index}
                className="bg-if-paper rounded-lg shadow-if border border-if-medium/30 p-6 hover:border-if-hl-medium/50 transition-colors"
              >
                <div className="flex items-center justify-between">
                  <div className="flex-1">
                    <h3 className="text-lg font-medium text-if-dark mb-1">{download.name}</h3>
                    <p className="text-if-medium text-sm mb-2">{download.description}</p>
                    <div className="flex gap-4 text-sm text-if-medium">
                      <span>Version: <strong className="text-if-dark">{download.version}</strong></span>
                      <span>Size: <strong className="text-if-dark">{formatFileSize(download.size)}</strong></span>
                      {download.platform && (
                        <span>Platform: <strong className="text-if-dark">{download.platform}</strong></span>
                      )}
                    </div>
                  </div>
                  <button
                    onClick={() => handleDownload(download)}
                    disabled={downloading === download.filename}
                    className="flex items-center gap-2 px-6 py-3 bg-if-hl-medium text-white rounded-lg shadow-if-lg border border-if-dark hover:bg-if-hl-dark transition-colors disabled:opacity-50 disabled:cursor-wait"
                  >
                    {downloading === download.filename ? <SpinnerIcon /> : <DownloadIcon />}
                    <span className="font-medium">
                      {downloading === download.filename ? 'Downloading...' : 'Download'}
                    </span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        {/* Help Text */}
        <div className="mt-8 p-4 bg-if-light/10 border border-if-light/30 rounded-lg">
          <h4 className="font-medium text-if-dark mb-2">Need Help?</h4>
          <p className="text-sm text-if-medium">
            If you have any issues downloading or installing the software, please contact your system administrator.
          </p>
        </div>
      </div>

      {/* Footer */}
      <footer className="bg-[#333] text-if-window text-center py-2 text-sm">
        Infoforum Downloads
      </footer>
    </div>
  );
};

export default DownloadList;
