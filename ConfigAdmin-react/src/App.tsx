import { Routes, Route } from 'react-router-dom';
import { Header } from './components/Header';
import { ConfigList } from './components/ConfigList';
import { ConfigEditor } from './components/ConfigEditor';
import { ToastProvider } from './components/Toast';

function App() {
  return (
    <ToastProvider>
      <div className="app">
        <Header />
        <main className="if-container">
          <Routes>
            <Route path="/" element={<ConfigList />} />
            <Route path="/new" element={<ConfigEditor />} />
            <Route path="/edit/:idx" element={<ConfigEditor />} />
          </Routes>
        </main>
      </div>
    </ToastProvider>
  );
}

export default App;
