# Design Direction

Frontend visual redesign milestone (post-M7). Selected by Claude Code (Orchestrator)
jointly with the "Claude — Frontend / UI" peer session, based on real firsthand visual
research performed by the "Antigravity" agent (browser-based inspection of Inspora,
Refero Styles, and Spell — not desk research from memory). Full raw research and all
three candidate directions in complete detail are preserved in git history / session
logs; this document captures the decision, the rationale, and the rejected
alternatives per the brief.

This supersedes the previous version of this file, which was produced by desk
research only (no live browsing) because no research tool was connected at the time.
That earlier pass independently landed on the same conceptual direction
("Ledger-as-Instrument") that real visual research now confirms below — noted as a
convergence signal, not used as a reason to pick it on its own.

The full raw research (Antigravity's complete direction-by-direction writeup) and a
precise draft token spec for all three candidates existed as two extra files at repo
root (`DESIGN_RESEARCH_FINDINGS.md`, `DESIGN_TOKENS_PROPOSAL.md`) during this
milestone's research pass. Their Direction-1-relevant content is now folded into this
document and into `frontend/src/styles/tokens.css` directly; both files were removed,
uncommitted, before this document's own commit, rather than kept as a third and
fourth overlapping design doc — so this repo's history never carries the raw
Direction 2/3 detail (rejected) as a separate artifact. It's fully reproducible from
the same live sources (Refero Styles, Inspora, Spell) cited below if ever needed
again; it isn't preserved here on the assumption that it won't be.

## Selected: Direction 1 — "The Ledger Terminal"

A developer/quant-grade, high-density financial instrument (Bloomberg Terminal /
Linear / Brex-inspired). The interface does not hide the double-entry accounting
engine behind a simplified consumer gloss — it shows it. Both legs of every
transaction (debit + credit), running derived balances, and live
`Σ Debits = Σ Credits` verification are visible on the primary surfaces, not tucked
behind a detail view. This is the only one of the three candidates where the visual
language directly demonstrates the product's actual claim — balances are derived,
never stored, and the ledger is the source of truth — rather than merely avoiding a
generic look through restraint (Direction 2) or energy (Direction 3).

### Rationale
- Matches the user's explicitly requested palette (black/near-black, charcoal/
  graphite, warm gray, orange, amber/yellow, off-white/white) almost exactly, with no
  forcing — Direction 2 and 3's palettes required more interpretation to fit it.
- Highest scores on Antigravity's own comparison matrix: engineering credibility
  (5/5), portfolio differentiation (5/5), lowest risk of drifting into a recognizable
  clone of an existing fintech product.
- Lowest execution risk: borders + tables + `tabular-nums` typography is more
  reliably buildable to a high-polish bar than Direction 2's serif/warm-gradient
  pairing (which also needs open-font substitutes for its recommended commercial
  typefaces) or Direction 3's spring-physics bento grid.
- Direct product fit: this portfolio's engineering centerpiece is ledger
  correctness, not visual flash — Direction 1 is the direction where the UI argument
  and the engineering argument are the same argument.
- Confirmed independently by both the Orchestrator and the Frontend implementer
  after reading the full research against the actual current codebase.

### Rejected alternatives
- **Direction 2 — "Editorial Vault & Parchment"** (Swiss-private-banking / archival
  bookkeeping-journal aesthetic, serif headline balances, warm brass/terracotta
  accents). Genuinely distinctive and credible, but: doesn't match the requested
  palette without reinterpretation, needs commercial-font substitution
  (Ivy Presto/Flecha → Newsreader/Fraunces/Playfair/Instrument Serif), and doesn't
  visually demonstrate double-entry mechanics the way Direction 1 does — it earns
  trust through restraint rather than through showing the proof.
- **Direction 3 — "Neo-Kinetic Clean Treasury"** (Ramp/Brex/Awesomic-inspired
  bento-grid, spring-physics motion, electric orange/amber/mint accents). Highest
  polish ceiling but the highest risk of reading as "another modern fintech SaaS
  dashboard" per Antigravity's own scoring (3.5/5 differentiation, explicitly flagged
  as risking a Ramp/Mercury clone) — the thing this milestone is trying to move away
  from. Kinetic spring motion also sits close to the "gimmicky portfolio-demo
  behavior" the brief explicitly warns against.

## Palette

Implemented in `frontend/src/styles/tokens.css`. Dark is the unconditional `:root`
default (not gated behind `prefers-color-scheme: dark`) — light is the override,
under `prefers-color-scheme: light` — so dark is what actually renders by default,
matching "dark (primary theme)" rather than just being labeled that way while an
unset-OS-preference browser would still see paper.

**Dark (primary theme):**
| Token | Value | CSS variable | Use |
|---|---|---|---|
| Void Black | `#08090A` | `--color-bg` | App background |
| Carbon Plate | `#111215` | `--color-surface` | Panel / card surface |
| Graphite Card | `#181A1F` | `--color-surface-elevated` | Stepped-up surface (nested panels — e.g. the 3-column workspace's inspector) |
| — | `#060708` | `--color-surface-sunken` | Recessed surface (hover rows, sunken inputs) |
| Hairline Border | `#242730` | `--color-border` | Structural borders/rules |
| — | `#3D4454` | `--color-border-strong` | Emphasized borders (panel edges, active dividers) |
| Crisp White | `#F0F2F5` | `--color-text` | Primary text; also the Debit-leg value color (neutral ink, no accent) |
| Muted Ash | `#8B909A` | `--color-text-muted` | Secondary text |
| Faint Hairline | `#484D58` | `--color-text-faint` | Tertiary/disabled text |
| Precision Ember | `#FF5500` | `--color-accent` / `--color-accent-strong: #FF7A33` | Money-in-motion only — primary CTAs, active transfer state |
| Ledger Amber | `#FFB020` | `--color-pending` | Pending states, idempotency keys, reconciliation warnings |
| Balanced Emerald | `#00C853` | `--color-credit` | Zero-drift verification *and* the Credit-leg value color — same semantic (positive/clean), reusing this file's pre-existing `credit` = "balanced" convention (see `ReconciliationPage`'s clean banner) rather than adding a fourth accent |

**Light theme (not an inversion — separately designed):**
| Token | Value | CSS variable | Use |
|---|---|---|---|
| Cold Paper | `#F8F9FA` | `--color-bg` | App background |
| Crisp Slate | `#FFFFFF` | `--color-surface` | Panel / card surface |
| — | `#F1F3F5` | `--color-surface-elevated` | Stepped-up surface |
| — | `#DEE2E6` | `--color-surface-sunken` | Recessed surface |
| Border | `#D8DEE4` | `--color-border` | Structural borders/rules |
| — | `#ADB5BD` | `--color-border-strong` | Emphasized borders |
| Deep Charcoal Ink | `#16181D` | `--color-text` | Primary text / Debit-leg value color |
| — | `#495057` | `--color-text-muted` | Secondary text |
| — | `#868E96` | `--color-text-faint` | Tertiary/disabled text |
| Ember (light-tuned) | `#E04B00` | `--color-accent` / `--color-accent-strong: #B93D00` | Same role as dark, contrast-checked against white |
| Amber (light-tuned) | `#D97706` | `--color-pending` | Same role |
| Emerald (light-tuned) | `#059669` | `--color-credit` | Same role |

`--color-danger` (`#FF4D4F` dark / `#DC2626` light) exists as a fourth semantic color
for hard failures (reversal errors, validation) — distinct from the Ember/Amber/
Emerald trio above, which is reserved strictly for ledger-state signals per the
"never decorative" rule below.

Color discipline: the accent trio is never decorative. Ember appears only on
actionable money-moving elements; Amber only on pending/warning states; Emerald
only on verified-balanced states. Status is always paired with an icon + text label,
never color alone (WCAG requirement carried over from the original research).

Radius is `--radius-sm: 1px` / `--radius-md: 2px` (both themes, "zero to 2px" per
this direction). `--shell-max-width` is `1440px`, up from the previous 1180px, to
close most of the ~50% viewport void the original research flagged — though the
full edge-to-edge 3-column workspace layout is separate, later page-level work, not
part of the token rewrite.

## Typography

- **UI sans:** Geist Sans (or IBM Plex Sans as fallback) — medium/semibold, tight
  tracking (`-0.015em`) for labels, nav, body text.
- **Ledger monospace:** Geist Mono (or IBM Plex Mono) with
  `font-variant-numeric: tabular-nums` applied to all currency amounts, timestamps,
  idempotency keys, and account/transaction IDs — signals "machine-verifiable value,"
  not decoration.

## Layout & component language

- Edge-to-edge desktop layout, `max-width: 1440px` (current app wastes ~50% of a
  1080p/1440p viewport inside a fixed 1126px centered column — this is corrected).
- Slim structural command rail (not an icon sidebar) with a live telemetry indicator
  and labeled nav; `⌘/Ctrl-K` command palette as a supplement, never a replacement.
- Zero to 2px border-radius. 1px structural borders (`var(--border)`). No drop
  shadows — depth via surface stepping (background → elevated → card) instead.
- Wallets presented as "Account Nodes": name + monospaced UUID (with copy action),
  large tabular balance, a real telemetry subline (entry count, last activity) —
  never an isolated stat tile divorced from the ledger that produced it.
- Transactions show both entry legs (Debit + Credit), not just the caller's side,
  plus the running derived balance after each entry.
- Transfer flow: explicit FROM / TO / AMOUNT / CURRENCY, a live preview of the exact
  ledger entries about to be posted, explicit idempotency key visibility, before
  commit.
- Mobile (<640px): the existing `Table.module.css` stacked-row pattern (a
  `data-label`-driven labeled flex row, no JS, already screen-reader-correct) is
  extended to emit two labeled sub-rows (Debit leg / Credit leg) per transaction
  block, rather than introducing a new accordion/collapsible component. Cheaper to
  build, and consistent with this direction's own "information re-flowed, never
  hidden" rule — an explicit revision Antigravity's raw research did not consider,
  made jointly by the Orchestrator and Frontend implementer against the real
  codebase.

## Interaction / motion principles

Near-zero ambient motion. 80–120ms transitions. Values update instantly or via a
brief numeric tick/slide on change. No card transitions, no parallax, no decorative
easing — motion is only ever a signal that something changed, never ambience.
Respect `prefers-reduced-motion`.

## Source inspirations (from Antigravity's live research)

- **Refero Styles:** Linear ("midnight precision instrument" — density without
  clutter, monospaced metadata, `⌘K`) combined with Brex ("white concrete, single
  ember" — extreme restraint, accent reserved strictly for execution/state triggers).
- **Inspora:** `/posts/composer-mockup` (dense dual-pane workspace composition),
  `/posts/sidebar-active-state` (tight sliding-pill nav indicator on dark rails).
- **Spell:** `kbd` keyboard-shortcut badges for nav/actions, Copy Button for wallet
  UUIDs/transaction IDs/idempotency keys, Flow Button for the transfer-commit action.

## Next steps

Token rewrite (`frontend/src/styles/tokens.css` → this palette; delete the dead
default-template `index.css`) is the first implementation task, owned by
Claude — Frontend / UI. Subsequent redesign work proceeds page by page against this
document. Real-browser QA (both themes, representative viewports) is performed by
Antigravity once pages are implemented, per the milestone's workflow.
