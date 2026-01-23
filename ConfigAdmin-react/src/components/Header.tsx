import { Link } from 'react-router-dom';
import './Header.css';

export function Header() {
  return (
    <header className="if-header">
      <div className="header-inner">
        <Link to="/" className="if-header-left">
          <img src="/IF-Logo.png" alt="IF" className="if-logo" />
          <span>Config Admin</span>
          <span className="if-version-label">v1.0</span>
        </Link>
        <nav>
          <Link to="/new" className="if-btn if-btn-warning">
            + New Entry
          </Link>
        </nav>
      </div>
    </header>
  );
}
