import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { defineConfig, type DefaultTheme } from "vitepress";

const docsDir = join(import.meta.dirname, "..");

const aboutDecisions = "/decisions";

function scanDecisionItems(folder: string): DefaultTheme.SidebarItem[] {
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
        items: scanDecisionItems("architecture/decisions"),
    },
];

const changesSidebar: DefaultTheme.SidebarItem[] = [
    {
        text: "Changes",
        items: [
            { text: "Overview", link: "/changes/" },
            { text: "Authentication", link: "/changes/authentication" },
            { text: "Sends", link: "/changes/sends" },
        ],
    },
    {
        text: "Decisions",
        collapsed: true,
        items: scanDecisionItems("changes/decisions"),
    },
];

export default defineConfig({
    title: "BigFatFile",
    description: "Design documentation for BigFatFile v2",
    cleanUrls: true,
    lastUpdated: true,
    themeConfig: {
        nav: [
            { text: "Architecture", link: "/architecture/" },
            { text: "Changes", link: "/changes/" },
            { text: "Decisions", link: aboutDecisions },
        ],
        sidebar: {
            "/architecture/": architectureSidebar,
            "/changes/": changesSidebar,
        },
        outline: { level: [2, 3] },
        search: { provider: "local" },
    },
});
