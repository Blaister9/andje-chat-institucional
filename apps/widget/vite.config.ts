import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    port: 5174,
    strictPort: true,
  },
  build: {
    lib: {
      entry: 'src/andje-chat-widget.ts',
      name: 'AndjeChatWidget',
      formats: ['iife'],
      fileName: () => 'andje-chat-widget.js',
    },
  },
});
