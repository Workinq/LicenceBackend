import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { TanStackRouterVite } from '@tanstack/router-plugin/vite';

export default defineConfig({
  plugins: [
    TanStackRouterVite({ routesDirectory: './src/routes', generatedRouteTree: './src/routeTree.gen.ts' }),
    react(),
    tailwindcss(),
  ],
  server: {
    port: 5173,
    proxy: {
      '/sessions': { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/licences':  { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/products':  { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/users':     { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/me':        { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/health':    { target: 'https://localhost:5001', changeOrigin: true, secure: false },
      '/openapi':   { target: 'https://localhost:5001', changeOrigin: true, secure: false },
    },
  },
  build: {
    outDir: 'dist',
  },
});
