/**
 * IF Design System - Tailwind Preset
 * 
 * Usage in tailwind.config.js:
 * 
 * import ifPreset from '@if/styles/tailwind-preset';
 * 
 * export default {
 *   presets: [ifPreset],
 *   content: [...],
 * }
 */

/** @type {import('tailwindcss').Config} */
export default {
  theme: {
    extend: {
      colors: {
        // Primary IF palette
        'if-light': '#1d97a6',
        'if-medium': '#214c8c',
        'if-dark': '#0b2144',
        
        // Highlight/accent colours
        'if-hl-light': '#faa236',
        'if-hl-medium': '#fa7733',
        'if-hl-dark': '#db592e',
        
        // Background colours
        'if-window': 'rgb(245, 245, 245)',
        'if-paper': 'rgb(252, 252, 252)',
      },
      fontFamily: {
        sans: ['Roboto', 'Helvetica Neue', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'Consolas', 'monospace'],
      },
      boxShadow: {
        'if': '0 2px 6px rgba(11, 33, 68, 0.15)',
        'if-lg': '2px 4px 6px rgba(11, 33, 68, 0.2)',
      },
      borderRadius: {
        'if-sm': '3px',
        'if-md': '5px',
        'if-lg': '10px',
      },
    },
  },
};
