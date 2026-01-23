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

## Step 4: Update component classes (optional)

The package uses `if-` prefixed classes. You can either:

### Option A: Use the new class names

```jsx
// Before
<button className="btn btn-primary">Save</button>

// After
<button className="if-btn if-btn-primary">Save</button>
```

### Option B: Keep existing class names

Add aliases in your app's CSS:

```css
@import "tailwindcss";
@import "@if/styles";

/* Aliases for backward compatibility */
@layer components {
  .btn { @apply if-btn; }
  .btn-primary { @apply if-btn-primary; }
  .btn-secondary { @apply if-btn-secondary; }
  .btn-warning { @apply if-btn-warning; }
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

## Example: SfdLogViewer Migration

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
@layer components {
  .log-table { ... }
  .badge-production { ... }
}
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
├── SfdLogViewer/
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
