// frontend/orval.config.ts
import { defineConfig } from 'orval';

export default defineConfig({
  licencebackend: {
    input: 'http://localhost:5001/openapi/v1.json',
    output: {
      target: './src/api/generated/api.ts',
      client: 'react-query',
      mode: 'split',
      override: {
        mutator: {
          path: './src/auth/api-client.ts',
          name: 'apiClient',
        },
      },
    },
  },
});
