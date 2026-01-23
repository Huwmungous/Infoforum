# @if/styles

Shared IF design system styles for React applications.

## Installation

Add to your project as a local dependency in `package.json`:

```json
{
  "dependencies": {
    "@if/styles": "file:../../IF.Styles"
  }
}
```

Then run `npm install`.

## Usage

### With Tailwind CSS 4

In your main CSS file (e.g., `src/index.css`):

```css
@import "tailwindcss";
@import "@if/styles";
```

This imports:
- Tailwind 4 `@theme` colour definitions
- CSS custom properties
- Base styles (body, scrollbars, focus states)
- Component classes

**Important: @apply Limitation**

In Tailwind CSS 4, you cannot use `@apply` with custom component classes (like `if-btn`, `if-card`, etc.) from imported packages. Use the classes directly in your HTML/JSX instead:

```jsx
// ✓ Correct - use classes directly
<button className="if-btn if-btn-primary">Click me</button>

// ✗ Won't work in Tailwind 4
.my-button { @apply if-btn if-btn-primary; }
```

If you need custom variations, write the CSS properties directly using the IF CSS variables.

### Importing Specific Parts

You can import only what you need:

```css
@import "tailwindcss";
@import "@if/styles/base";       /* Just base styles and tokens */
@import "@if/styles/components"; /* Just component classes */
```

### Using the Tailwind Preset (Alternative)

For projects not using Tailwind 4's `@theme`, you can use the preset:

```js
// tailwind.config.js
import ifPreset from '@if/styles/tailwind-preset';

export default {
  presets: [ifPreset],
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
}
```

### Using Assets

Copy the IF logo to your public folder, or reference it from the package:

```jsx
// Option 1: Copy to public folder (recommended)
<img src="/IF-Logo.png" alt="IF" className="if-logo" />

// Option 2: Import directly
import logo from '@if/styles/assets/IF-Logo.png';
<img src={logo} alt="IF" className="if-logo" />
```

### Required Google Fonts

Add to your `index.html`:

```html
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600&family=Roboto:wght@400;500;700&display=swap" rel="stylesheet">
```

## Colour Palette

### Primary Colours

| Name | Hex | CSS Variable | Tailwind |
|------|-----|--------------|----------|
| IF Light | `#1d97a6` | `var(--if-light-colour)` | `if-light` |
| IF Medium | `#214c8c` | `var(--if-medium-colour)` | `if-medium` |
| IF Dark | `#0b2144` | `var(--if-dark-colour)` | `if-dark` |

### Highlight/Accent Colours

| Name | Hex | CSS Variable | Tailwind |
|------|-----|--------------|----------|
| IF HL Light | `#faa236` | `var(--if-hl-light-colour)` | `if-hl-light` |
| IF HL Medium | `#fa7733` | `var(--if-hl-medium-colour)` | `if-hl-medium` |
| IF HL Dark | `#db592e` | `var(--if-hl-dark-colour)` | `if-hl-dark` |

### Background Colours

| Name | Value | CSS Variable | Tailwind |
|------|-------|--------------|----------|
| IF Window | `rgb(245, 245, 245)` | `var(--if-window-colour)` | `if-window` |
| IF Paper | `rgb(252, 252, 252)` | `var(--if-paper-colour)` | `if-paper` |

## Component Classes

### Layout

- `.if-app-container` - Full height flex container
- `.if-container` - Centered content container (max 1400px)
- `.if-header` - Dark header bar
- `.if-footer` - Dark footer bar

### Buttons

```html
<button class="if-btn if-btn-primary">Primary</button>
<button class="if-btn if-btn-secondary">Secondary</button>
<button class="if-btn if-btn-warning">Warning</button>
<button class="if-btn if-btn-danger">Danger</button>
<button class="if-btn if-btn-success">Success</button>
<button class="if-btn if-btn-ghost">Ghost</button>

<!-- Sizes -->
<button class="if-btn if-btn-primary if-btn-sm">Small</button>
<button class="if-btn if-btn-primary if-btn-lg">Large</button>
```

### Form Elements

```html
<div class="if-form-group">
  <label class="if-form-label">Label</label>
  <input class="if-form-input" type="text" placeholder="Input...">
</div>

<select class="if-form-select">
  <option>Option 1</option>
</select>

<!-- Header-style (dark background) -->
<select class="if-header-select">...</select>
<input class="if-header-input" placeholder="Search...">

<!-- Toggle switch -->
<button class="if-toggle active"></button>
```

### Cards

```html
<div class="if-card">
  <div class="if-card-header">
    <h2>Title</h2>
  </div>
  <div class="if-card-body">
    Content here
  </div>
  <div class="if-card-footer">
    Footer content
  </div>
</div>
```

### Tables

```html
<table class="if-table">
  <thead>
    <tr>
      <th>Column</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Data</td>
    </tr>
  </tbody>
</table>
```

### Badges

```html
<span class="if-badge if-badge-enabled">Enabled</span>
<span class="if-badge if-badge-disabled">Disabled</span>
<span class="if-badge if-badge-info">Info</span>
<span class="if-badge if-badge-warning">Warning</span>
<span class="if-badge if-badge-danger">Danger</span>
```

### Tabs

```html
<div class="if-tabs">
  <button class="if-tab active">Tab 1</button>
  <button class="if-tab">Tab 2</button>
  <button class="if-tab">Tab 3</button>
</div>
```

### Modals

```html
<div class="if-modal-overlay">
  <div class="if-modal">
    <div class="if-modal-header">
      <h2>Modal Title</h2>
    </div>
    <div class="if-modal-body">
      Content
    </div>
    <div class="if-modal-footer">
      <button class="if-btn if-btn-secondary">Cancel</button>
      <button class="if-btn if-btn-primary">Save</button>
    </div>
  </div>
</div>
```

### Toasts

```html
<div class="if-toast-container">
  <div class="if-toast if-toast-success">Success message</div>
  <div class="if-toast if-toast-error">Error message</div>
  <div class="if-toast if-toast-info">Info message</div>
  <div class="if-toast if-toast-warning">Warning message</div>
</div>
```

### Utilities

```html
<span class="if-text-muted">Muted text</span>
<code class="if-text-mono">Monospace text</code>
<div class="if-truncate">Long text that will be truncated...</div>
<div class="if-spinner"></div>
<div class="if-animate-new">Newly added item</div>
```

## Dark Mode

Add the `dark` class to the `<body>` element to enable dark mode:

```html
<body class="dark">
```

The design system automatically adjusts colours for dark mode.

## Directory Structure

```
@if/styles/
├── package.json
├── README.md
├── src/
│   ├── index.css          # Main entry (imports all)
│   ├── base.css           # Tokens and base styles
│   ├── components.css     # Component classes
│   └── tailwind-preset.js # Tailwind preset (alternative)
└── assets/
    └── IF-Logo.png        # IF logo
```
