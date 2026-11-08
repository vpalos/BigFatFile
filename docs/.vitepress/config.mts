import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig, type DefaultTheme } from "vitepress";

const docsDir = join(import.meta.dirname, "..");

// Records are listed from the folder, so a new record needs no change here.
function decisionItems(): DefaultTheme.SidebarItem[] {
    const dir = join(docsDir, "decisions");
    return readdirSync(dir)
        .filter(
            (file) => /^\d{3}-.+\.md$/.test(file) && !file.startsWith("000-"),
        )
        .sort()
        .map((file) => {
            const heading = readFileSync(join(dir, file), "utf-8").match(
                /^# (.+)$/m,
            );
            // Record 001 is the one that explains the records.
            const title = heading ? heading[1] : file;
            return {
                text: file.startsWith("001-") ? "About decisions" : title,
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
            { text: "Topology", link: "/architecture/platform/topology" },
            { text: "API", link: "/architecture/platform/api" },
            { text: "Licensing", link: "/architecture/platform/licensing" },
        ],
    },
    {
        text: "Security",
        collapsed: false,
        items: [
            { text: "Tenancy", link: "/architecture/security/tenancy" },
            {
                text: "Authentication",
                link: "/architecture/security/authentication",
            },
        ],
    },
    {
        text: "Files",
        collapsed: false,
        items: [
            { text: "Data model", link: "/architecture/files/data-model" },
            { text: "File storage", link: "/architecture/files/storage" },
            { text: "File transfer", link: "/architecture/files/transfer" },
            {
                text: "File materialisation",
                link: "/architecture/files/materialisation",
            },
            {
                text: "Client synchronization",
                link: "/architecture/files/synchronization",
            },
        ],
    },
    {
        text: "Operations",
        collapsed: false,
        items: [
            { text: "Deployment", link: "/architecture/operations/deployment" },
            {
                text: "Observability",
                link: "/architecture/operations/observability",
            },
        ],
    },
    {
        text: "Decisions",
        collapsed: true,
        items: decisionItems(),
    },
];

export default defineConfig({
    title: "BigFatFile",
    description: "Architecture documents for BigFatFile v2",
    cleanUrls: true,
    lastUpdated: true,
    srcExclude: ["decisions/000-template.md"],
    // Mermaid is one large chunk, loaded only on pages that have a diagram.
    vite: { build: { chunkSizeWarningLimit: 4000 } },
    themeConfig: {
        nav: [
            { text: "Architecture", link: "/architecture/" },
            {
                text: "Decisions",
                link: "/decisions/001-architecture-decision-records",
            },
        ],
        sidebar,
        outline: { level: [2, 3] },
        search: { provider: "local" },
    },
});
