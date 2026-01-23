# Migrating Apps to @if/styles

This document shows how to update your existing apps to use the shared @if/styles package.

## Step 1: Add the dependency

In your app's `package.json`, add:

```json
{
  "dependencies": {
    "@if/styles": "file:../../IF.Styles"
  }
}
```

Adjust the path based on your project structure.

## Step 2: Update index.css

Replace your app's `src/index.css` with:

```css
@import "tailwindcss";
@import "@if/styles";

/* App-specific styles go here (if any) */
```

## Step 3: Copy the logo (if not already present)

Copy `IF-Logo.png` from the package to your `public/` folder:

```bash
cp node_modules/@if/styles/assets/IF-Logo.png public/
```

## Step 4: Update component classes

The package uses `if-` prefixed classes. Update your components to use the new class names:

```jsx
// Before
<button className="btn btn-primary">Save</button>

// After
<button className="if-btn if-btn-primary">Save</button>
```

**Important**: In Tailwind CSS 4, you cannot use `@apply` with custom component classes from imported packages. You must use the classes directly in your HTML/JSX.

```css
/* ✗ This won't work in Tailwind 4 */
.btn { @apply if-btn; }
.btn-primary { @apply if-btn-primary; }

/* ✓ Instead, use the if-* classes directly in your components */
```

If you need custom variations, write the CSS properties directly using the IF CSS variables:

```css
.my-custom-button {
  padding: var(--if-space-sm) var(--if-space-md);
  background: var(--if-light-colour);
  color: white;
  border-radius: var(--if-radius-md);
  /* etc. */
}
```

## Example: ConfigAdmin Migration

### Before (src/index.css)

```css
:root {
  --if-light-colour: #1d97a6;
  --if-medium-colour: #214c8c;
  /* ... 200+ lines of CSS ... */
}
```

### After (src/index.css)

```css
@import "tailwindcss";
@import "@if/styles";

/* App-specific overrides only */
.config-list { ... }
.json-editor { ... }
```

## Example: IFLogViewer Migration

### Before (src/index.css)

```css
@import "tailwindcss";

@custom-variant dark (&:where(.dark, .dark *));

@theme {
  --color-if-light: #1d97a6;
  /* ... lots of theme and component definitions ... */
}
```

### After (src/index.css)

```css
@import "tailwindcss";
@import "@if/styles";

/* App-specific classes only */
.log-table { ... }
.badge-production { ... }
```

## Project Structure

Recommended folder layout:

```
repos/
├── IF.Styles/              # The shared styles package
│   ├── package.json
│   ├── src/
│   │   ├── index.css
│   │   ├── base.css
│   │   └── components.css
│   └── assets/
│       └── IF-Logo.png
│
├── ConfigAdmin/
│   ├── package.json        # "@if/styles": "file:../IF.Styles"
│   └── src/
│       └── index.css       # @import "@if/styles";
│
├── IFLogViewer/
│   ├── package.json        # "@if/styles": "file:../IF.Styles"
│   └── src/
│       └── index.css       # @import "@if/styles";
│
└── display-token-app/
    ├── package.json        # "@if/styles": "file:../IF.Styles"
    └── src/
        └── index.css       # @import "@if/styles";
```

## Benefits

1. **Single source of truth** - Update colours/styles in one place
2. **Consistency** - All apps automatically use the same design
3. **Smaller bundles** - No duplicated CSS definitions
4. **Easier maintenance** - Fix bugs or add features once
5. **Versioning** - Can version the package and update apps incrementally
