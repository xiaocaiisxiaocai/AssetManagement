# Repository Guidelines

## Project Structure & Module Organization

This repository contains documentation and a static prototype for the department asset management system.

- `docs/`: requirements, architecture, design notes, and prototype specification documents.
- `prototype/index.html`: single-page prototype entry point.
- `prototype/css/style.css`: global styling for layout, forms, tables, dialogs, and responsive behavior.
- `prototype/js/data.js`: mock data used by the prototype.
- `prototype/js/pages.js`: page template generators.
- `prototype/js/app.js`: navigation, modal, filtering, and interaction logic.

Keep product decisions in `docs/` and runnable prototype changes in `prototype/`.

## Build, Test, and Development Commands

There is no package manager or build pipeline in the current repo. Run the prototype as static files.

- `cd prototype && python -m http.server 8080`: serve the prototype locally.
- Open `http://localhost:8080/`: view and manually test the app.
- `Start-Process .\prototype\index.html`: quick Windows-only direct browser preview.

Use the local server for normal checks because browser security rules can differ for direct file access.

## Coding Style & Naming Conventions

Use plain HTML, CSS, and vanilla JavaScript. Match the existing 4-space indentation in HTML/CSS/JS files. Prefer clear, direct functions over new abstractions unless repeated logic justifies extraction.

- JavaScript functions and variables: `camelCase`, e.g. `handleLogin`, `currentFilters`.
- Page keys: kebab-case strings, e.g. `asset-list`, `approval-pending`.
- CSS classes and IDs: kebab-case, e.g. `.login-container`, `#page-main`.
- Keep mock records in `data.js`; avoid hard-coding shared sample data inside templates.

## Testing Guidelines

No automated test framework is configured. Before submitting changes, perform a browser smoke test:

- Login flow from `page-login` to `page-main`.
- Main navigation across asset, approval, report, and admin pages.
- Filters, reset buttons, pagination controls, dialogs, and batch action alerts.
- Responsive layout at desktop and mobile widths.

Document any untested area in the pull request.

## Commit & Pull Request Guidelines

This directory is not currently a Git repository, so no local commit history is available. Use concise Conventional Commits when version control is added, for example `feat: add asset import modal` or `fix: correct overdue report filter`.

Pull requests should include the change purpose, affected screens or documents, manual test notes, and screenshots or screen recordings for visible UI changes.

## Security & Configuration Tips

Do not commit real employee data, production credentials, or internal system endpoints. Keep prototype-only sample data clearly synthetic and localized to `prototype/js/data.js`.
