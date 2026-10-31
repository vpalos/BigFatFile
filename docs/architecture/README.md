# BigFatFile docs

This folder is a [docsify](https://docsify.js.org) site. The Markdown files are the
content; `index.html` renders them in the browser, and `_sidebar.md` is the navigation.

To read the docs as a site, serve this folder over HTTP and open it in a browser:

```bash
python3 -m http.server 3000 --directory docs/architecture
```

Then open http://localhost:3000. The start page is `Architecture.md`.

Opening `index.html` directly from disk does not work: docsify fetches the Markdown
files with HTTP requests, which browsers block for `file://` pages.

To add a page, create a Markdown file here and add a line for it in `_sidebar.md`.
