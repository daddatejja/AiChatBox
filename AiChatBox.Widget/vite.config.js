import { defineConfig } from 'vite';
import { resolve } from 'path';
import fs from 'fs';

function copyBuildPlugin() {
  return {
    name: 'copy-build-plugin',
    closeBundle() {
      const src = resolve(__dirname, 'dist/ai-chatbox.js');
      const srcMap = resolve(__dirname, 'dist/ai-chatbox.js.map');
      
      const targets = [
        resolve(__dirname, 'ai-chatbox.js'),
        resolve(__dirname, '../AiChatBox.Api/wwwroot/widget/ai-chatbox.js')
      ];

      targets.forEach(dest => {
        try {
          fs.copyFileSync(src, dest);
          console.log(`Copied build successfully to: ${dest}`);
        } catch (e) {
          console.error(`Failed to copy build to ${dest}:`, e);
        }
      });

      // Also copy source map
      try {
        fs.copyFileSync(srcMap, resolve(__dirname, 'ai-chatbox.js.map'));
        fs.copyFileSync(srcMap, resolve(__dirname, '../AiChatBox.Api/wwwroot/widget/ai-chatbox.js.map'));
      } catch (e) {}
    }
  };
}

export default defineConfig({
  plugins: [copyBuildPlugin()],
  build: {
    outDir: 'dist',
    lib: {
      entry: resolve(__dirname, 'src/index.js'),
      name: 'AiChatBoxWidget',
      fileName: () => 'ai-chatbox.js',
      formats: ['iife']
    },
    rollupOptions: {
      external: [],
      output: {
        extend: true,
        assetFileNames: (assetInfo) => {
          if (assetInfo.name === 'style.css') return 'ai-chatbox.css';
          return assetInfo.name;
        }
      }
    },
    minify: 'esbuild',
    sourcemap: true
  }
});
