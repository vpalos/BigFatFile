import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig, type DefaultTheme } from "vitepress";

const docsDir = join(import.meta.dirname, "..");

const aboutDecisions = "/decisions";

// Records are listed from their folder, so a new record needs no change here.
function decisionItems(folder: string): DefaultTheme.SidebarItem[] {
    const dir = join(docsDir, folder);
    const records = readdirSync(dir)
        .filter((file) => /^\d{3}-.+\.md$/.test(file))
        .sort()
        .map((file) => {
            const heading = readFileSync(join(dir, file), "utf-8").match(
                /^# (.+)$/m,
            );
            return {
                text: heading ? heading[1] : file,
                link: `/${folder}/${file.replace(/\.md$/, "")}`,
            };
        });
    return [{ text: "About decisions", link: aboutDecisions }, ...records];
}

const architectureSidebar: DefaultTheme.SidebarItem[] = [
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
        items: decisionItems("architecture/decisions"),
    },
];

const featuresSidebar: DefaultTheme.SidebarItem[] = [
    {
        text: "Features",
        items: [
            { text: "Overview", link: "/features/" },
            { text: "Sends", link: "/features/sends" },
        ],
    },
    {
        text: "Decisions",
        collapsed: true,
        items: decisionItems("features/decisions"),
    },
];

export default defineConfig({
    title: "BigFatFile",
    description: "Architecture and features of BigFatFile v2",
    cleanUrls: true,
    lastUpdated: true,
    // Mermaid is one large chunk, loaded only on pages that have a diagram.
    vite: { build: { chunkSizeWarningLimit: 4000 } },
    themeConfig: {
        nav: [
            { text: "Architecture", link: "/architecture/" },
            { text: "Features", link: "/features/" },
            { text: "Decisions", link: aboutDecisions },
        ],
        sidebar: {
            "/architecture/": architectureSidebar,
            "/features/": featuresSidebar,
        },
        outline: { level: [2, 3] },
        search: { provider: "local" },
    },
});
