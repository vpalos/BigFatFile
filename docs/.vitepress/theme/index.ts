import { h, nextTick, onMounted, watch } from "vue";
import { useData } from "vitepress";
import DefaultTheme from "vitepress/theme";
import type { Theme } from "vitepress";
import { createMermaidRenderer } from "vitepress-mermaid-renderer";
import { sizeDiagramsToContent } from "./diagramHeight";
import "./custom.css";

export default {
  extends: DefaultTheme,
  Layout: () => {
    const { isDark } = useData();

    const renderDiagrams = () => {
      const renderer = createMermaidRenderer({
        theme: isDark.value ? "dark" : "forest",
      });
      renderer.setToolbar({ fullscreenMode: "browser" });
    };

    nextTick(renderDiagrams);
    watch(isDark, renderDiagrams);

    return h(DefaultTheme.Layout);
  },
  setup() {
    onMounted(sizeDiagramsToContent);
  },
} satisfies Theme;
