import llmstxt from "vitepress-plugin-llms";
import { defineConfig } from "vitepress";

const repo = "https://github.com/AquaWallet/btcpay-plugin-indra";

// https://vitepress.dev/reference/site-config
export default defineConfig({
  title: "Indra BTCPay Plugin",
  description: "Indra BTCPay Plugin Docs",
  // Served from GitHub Pages under the repository name
  base: "/btcpay-plugin-indra/",
  head: [["link", { rel: "icon", href: "/btcpay-plugin-indra/assets/logo.png" }]],
  themeConfig: {
    logo: "/assets/logo.png",
    search: {
      provider: "local",
      options: {
        detailedView: true,
      },
    },
    sidebar: [
      {
        items: [
          { text: "👋 Introduction", link: "/index" },
          { text: "🔀 Moving from Boltz", link: "/moving-from-boltz" },
          { text: "🚧 Limitations", link: "/limitations" },
          { text: "🏗️ Building the Plugin", link: "/building-the-plugin" },
          { text: "🧪 Regtest Setup", link: "/regtest-setup" },
        ],
      },
    ],
    socialLinks: [{ icon: "github", link: repo }],
  },
  // Ignore dead links to localhost
  ignoreDeadLinks: [/https?:\/\/localhost/],
  vite: {
    plugins: [llmstxt({ excludeIndexPage: false })],
  },
});
