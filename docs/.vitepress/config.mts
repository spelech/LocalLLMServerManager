import { defineConfig } from 'vitepress';

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: 'Local LLM Server Manager',
  description: 'User Guide and Documentation for Local AI Engines, Multimodal Studio, and MCP Tools',
  base: '/LocalLLMServerManager/',
  cleanUrls: true,
  srcExclude: [
    'superpowers/**',
    'legacy-web-dash/**'
  ],

  themeConfig: {
    logo: '/images/logo.png',
    siteTitle: 'Local LLM Server Manager',

    nav: [
      { text: 'Guide', link: '/getting-started/installation' },
      { text: 'Engines', link: '/engines/' },
      { text: 'Studio', link: '/studio/' },
      { text: 'AI & MCP', link: '/ai-and-mcp/' },
      { text: 'Technical Reference', link: '/technical/' },
      { text: 'Style Guide', link: '/standards/ste-100' }
    ],

    sidebar: {
      '/getting-started/': [
        {
          text: 'Getting Started',
          items: [
            { text: 'Overview & Requirements', link: '/getting-started/' },
            { text: 'Installation', link: '/getting-started/installation' },
            { text: 'Configuration', link: '/getting-started/configuration' },
            { text: 'Quickstart Guide', link: '/getting-started/quickstart' },
            { text: 'Real Engine Test Flight', link: '/getting-started/test-flight' },
            { text: 'Remote Access & Reverse Proxy', link: '/getting-started/remote-access' },
            { text: 'Troubleshooting', link: '/getting-started/troubleshooting' }
          ]
        }
      ],
      '/engines/': [
        {
          text: 'Engines & Model Management',
          items: [
            { text: 'Engines Overview & VRAM', link: '/engines/' },
            { text: 'Ollama LLM Engine', link: '/engines/ollama' },
            { text: 'Stable Diffusion Forge', link: '/engines/sd-forge' },
            { text: 'ComfyUI Engine', link: '/engines/comfyui' },
            { text: 'Kokoro TTS Engine', link: '/engines/kokoro-tts' },
            { text: 'Model Hubs & Downloads', link: '/engines/model-management' }
          ]
        }
      ],
      '/studio/': [
        {
          text: 'Multimodal Studio',
          items: [
            { text: 'Studio Overview', link: '/studio/' },
            { text: 'Image Generation', link: '/studio/image-generation' },
            { text: 'LoRA Art Styles & CivitAI', link: '/studio/lora-styles' },
            { text: 'Video Generation', link: '/studio/video-generation' },
            { text: 'Audio & Music Synthesis', link: '/studio/audio-and-music' },
            { text: '3D Mesh Reconstruction', link: '/studio/3d-mesh' },
            { text: 'ComfyUI & 3D Setup', link: '/studio/comfyui-setup' }
          ]
        }
      ],
      '/ai-and-mcp/': [
        {
          text: 'AI & MCP Tools',
          items: [
            { text: 'Overview', link: '/ai-and-mcp/' },
            { text: 'AI Chat Assistant', link: '/ai-and-mcp/assistant' },
            { text: 'Magnetic Companion Windows', link: '/guide/companion-windows' },
            { text: 'Model Context Protocol (MCP)', link: '/ai-and-mcp/mcp-tools' },
            { text: 'Workflow Presets', link: '/ai-and-mcp/flows-and-presets' }
          ]
        }
      ],
      '/guide/': [
        {
          text: 'Technical Reference (Developers & Agents)',
          items: [
            { text: 'Technical Overview', link: '/technical/' },
            { text: 'System Architecture', link: '/technical/architecture' },
            { text: 'Development & Build Guide', link: '/technical/development' },
            { text: 'Magnetic Companion Windows', link: '/guide/companion-windows' },
            { text: 'Collaborative UI Debugging', link: '/guide/collaborative-debugging' },
            { text: 'Requirements Specification', link: '/technical/requirements' },
            { text: 'Windows Process Validation', link: '/technical/validation' },
            { text: 'Test Coverage Benchmarks', link: '/technical/test-coverage' },
            { text: 'AI Assistant Internals', link: '/technical/ai-assistant-internals' }
          ]
        }
      ],
      '/technical/': [
        {
          text: 'Technical Reference (Developers & Agents)',
          items: [
            { text: 'Technical Overview', link: '/technical/' },
            { text: 'System Architecture', link: '/technical/architecture' },
            { text: 'Development & Build Guide', link: '/technical/development' },
            { text: 'Magnetic Companion Windows', link: '/guide/companion-windows' },
            { text: 'Collaborative UI Debugging', link: '/guide/collaborative-debugging' },
            { text: 'Requirements Specification', link: '/technical/requirements' },
            { text: 'Windows Process Validation', link: '/technical/validation' },
            { text: 'Test Coverage Benchmarks', link: '/technical/test-coverage' },
            { text: 'AI Assistant Internals', link: '/technical/ai-assistant-internals' }
          ]
        }
      ],
      '/standards/': [
        {
          text: 'Writing Guidelines',
          items: [
            { text: 'Documentation Style Guide', link: '/standards/ste-100' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/spelech/LocalLLMServerManager' }
    ],

    search: {
      provider: 'local'
    },

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © Spelech'
    }
  }
});
