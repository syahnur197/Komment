import { defineConfig } from 'vite';
import tailwindcss from '@tailwindcss/vite';

// ponytail: CSS-only pipeline for now. main.js exists so Vite has a real entry;
// import JS modules from it when the Dashboard actually needs client-side code.
export default defineConfig({
  plugins: [tailwindcss()],
  build: {
    outDir: 'wwwroot/dist',
    emptyOutDir: true,
    rollupOptions: {
      input: 'Styles/main.js',
      // Stable names — Blazor's MapStaticAssets does the fingerprinting.
      output: {
        entryFileNames: 'main.js',
        assetFileNames: '[name][extname]',
      },
    },
  },
});
