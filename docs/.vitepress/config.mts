import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig, type DefaultTheme } from "vitepress";

const docsDir = join(import.meta.dirname, "..");

// Records are listed from the folder, so a new record needs no change here.
function decisionItems(): DefaultTheme.SidebarItem[] {
    const dir = join(docsDir, "decisions");
    return readdirSync(dir)
        .filter((file) => /^\d{4}-.+\.md$/.test(file) && !file.startsWith("0000-"))
        .sort()
        .map((file) => {
            const heading = readFileSync(join(dir, file), "utf-8").match(/^# (.+)$/m);
            return {
                text: heading ? heading[1] : file,
                link: `/decisions/${file.replace(/\.md$/, "")}`,
            };
        });
}

const sidebar: DefaultTheme.Sidebar = [
    {
        text: "Architecture",
        items: [
            { text: "Overview", link: "/architecture/" },
            { text: "Roadmap", link: "/architecture/roadmap" },
        ],
    },
    {
        text: "Platform",
        collapsed: false,
        items: [
            { text: "Platform topology", link: "/architecture/platform/topology" },
            { text: "API", link: "/architecture/platform/api" },
            { text: "Licensing", link: "/architecture/platform/licensing" },
        ],
    },
    {
        text: "Security",
        collapsed: false,
        items: [{ text: "Authentication", link: "/architecture/security/authentication" }],
    },
    {
        text: "Files",
        collapsed: false,
        items: [
            { text: "Data model", link: "/architecture/files/data-model" },
            { text: "File storage", link: "/architecture/files/storage" },
            { text: "File transfer", link: "/architecture/files/transfer" },
            { text: "File materialisation", link: "/architecture/files/materialisation" },
            { text: "Client synchronization", link: "/architecture/files/synchronization" },
        ],
    },
    {
        text: "Operations",
        collapsed: false,
        items: [
            { text: "Deployment", link: "/architecture/operations/deployment" },
            { text: "Observability", link: "/architecture/operations/observability" },
        ],
    },
    {
        text: "Decisions",
        collapsed: true,
        items: [{ text: "About the records", link: "/decisions/" }, ...decisionItems()],
    },
];

export default defineConfig({
    title: "BigFatFile",
    description: "Architecture documents for BigFatFile v2",
    cleanUrls: true,
    lastUpdated: true,
    srcExclude: ["decisions/0000-template.md"],
    // Mermaid is one large chunk, loaded only on pages that have a diagram.
    vite: { build: { chunkSizeWarningLimit: 4000 } },
    themeConfig: {
        nav: [
            { text: "Architecture", link: "/architecture/" },
            { text: "Decisions", link: "/decisions/" },
        ],
        sidebar,
        outline: { level: [2, 3] },
        search: { provider: "local" },
    },
});
