import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';

// Fonts — loaded via @fontsource, no CDN
import '@fontsource/inter/400.css';
import '@fontsource/inter/500.css';
import '@fontsource/inter/600.css';
import '@fontsource/fraunces/400.css';
import '@fontsource/fraunces/600.css';
import '@fontsource/jetbrains-mono/400.css';
import '@fontsource/jetbrains-mono/500.css';

import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <div className="min-h-screen bg-surface font-sans text-ink p-8">
      <h1 className="font-display text-3xl font-semibold">LicenceBackend Admin</h1>
      <p className="mt-2 text-ink-muted">Tailwind + fonts loaded.</p>
    </div>
  </StrictMode>,
);
