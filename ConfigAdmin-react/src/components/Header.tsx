import { Link } from 'react-router-dom';
import './Header.css';

export function Header() {
  return (
    <header className="header">
      <div className="header-inner">
        <Link to="/" className="logo">
          <img src="/IF-Logo.png" alt="IF" className="if-logo" />
          <span>Config Admin</span>
          <span className="version-number">v1.0</span>
        </Link>
        <nav>
          <Link to="/new" className="btn btn-warning">
            + New Entry
          </Link>
        </nav>
      </div>
    </header>
  );
}
