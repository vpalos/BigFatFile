import type { Theme } from "vitepress";
import { useData } from "vitepress";
import { createMermaidRenderer } from "vitepress-mermaid-renderer";
import DefaultTheme from "vitepress/theme";
import { h, nextTick, watch } from "vue";
import "./custom.css";

export default {
  extends: DefaultTheme,
  Layout: () => {
    const { isDark } = useData();

    const renderDiagrams = () => {
      const renderer = createMermaidRenderer({
        theme: isDark.value ? "redux-dark-color" : "redux-color",
      });
      renderer.setToolbar({ fullscreenMode: "browser" });
    };

    nextTick(renderDiagrams);
    watch(isDark, renderDiagrams);

    return h(DefaultTheme.Layout);
  },
} satisfies Theme;
