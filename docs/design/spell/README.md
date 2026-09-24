# Spell UI — real registry artifacts

Each `.json` file here is the **unmodified response** of Spell UI's own shadcn-compatible
component registry endpoint, `https://spell.sh/r/<component>.json` — the exact file their
own official installer (`pnpm dlx shadcn@latest add @spell/<component>`, shown on each
component's docs page under "Installation") fetches and writes into a project. Retrieved
2026-09-24 via direct HTTP GET, byte-for-byte as served, no AI paraphrase.

| File | Docs page | Official install command |
|---|---|---|
| `badge.json` | https://spell.sh/docs/badge | `pnpm dlx shadcn@latest add @spell/badge` |
| `copy-button.json` | https://spell.sh/docs/copy-button | `pnpm dlx shadcn@latest add @spell/copy-button` |
| `pop-button.json` | https://spell.sh/docs/pop-button | `pnpm dlx shadcn@latest add @spell/pop-button` |
| `kbd.json` | https://spell.sh/docs/kbd | `pnpm dlx shadcn@latest add @spell/kbd` |

## Why these weren't run through the CLI as-is

Every one of these components is a **shadcn/ui registry item**, not a standalone copy-paste
snippet. Reading the `dependencies` field of each JSON:

- `badge.json` → `@radix-ui/react-slot`, `class-variance-authority`
- `copy-button.json` → `lucide-react`
- `pop-button.json` → (none extra, but still Tailwind-classed)
- `kbd.json` → `react-hotkeys-hook`

All four component bodies are written in Tailwind utility classes (`bg-neutral-700`,
`dark:bg-neutral-200`, …) and import a `cn()` helper from `@/lib/utils` (the standard
shadcn boilerplate, itself `clsx` + `tailwind-merge`). This project has **no Tailwind CSS,
no shadcn `components.json`, no `@/lib/utils`, no path-alias config for `@/*`** — it's a
plain Vite + CSS Modules + hand-authored custom-properties (`tokens.css`) setup by design
(see `ARCHITECTURE.md`).

Running the literal install command would not "add a Badge" — it would bootstrap an entire
second styling system (Tailwind + Radix + CVA) alongside the existing one, and the Tailwind
default palette (`red-100`, `blue-100`, `neutral-700`, …) baked into these files has no
relationship to this project's actual design tokens (`--color-accent`, `--color-danger`,
etc.) or its near-black/graphite/amber identity. That's an architecture decision, not a
component-swap, so it's left for the person driving this milestone to choose rather than
being made silently mid-implementation. See the M8.2 checkpoint doc for the two concrete
options and their tradeoffs.
