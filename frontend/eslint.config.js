import { defineConfig, globalIgnores } from 'eslint/config';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';

export default defineConfig([
  globalIgnores(['dist', 'coverage']),
  {
    files: ['**/*.{ts,tsx,js}'],
    extends: [tseslint.configs.recommended, reactHooks.configs.flat['recommended-latest']],
  },
]);
