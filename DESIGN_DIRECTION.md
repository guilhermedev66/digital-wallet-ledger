# Design Direction

UX/visual research for the frontend (M5). Produced by the peer session assigned
UX Research / Frontend (see MEMORY.md "Multi-agent topology"). No dedicated
research tool ("Antigravity") exists in this environment — this is real desk
research done with WebSearch, synthesized against the product's actual
architecture (`ARCHITECTURE.md`).

**Research method / honesty note:** findings below come from web search result
snippets covering named products and design-blog case studies, not from
firsthand screenshots or scraped pages. Where a claim traces to a specific
source it's cited; where it's this session's own synthesis/extrapolation
(mainly all of Direction 3) it's flagged as such. Treat product specifics as
secondhand descriptions, not verified pixel references — worth a firsthand
look before copying anything literally.

## Why this matters for this product specifically

The brief is explicit: this is a **portfolio piece whose engineering
centerpiece is ledger correctness** — immutable entries, derived balances,
atomic transfers, reconciliation — not visual flash, and the user explicitly
does not want the frontend to read as another generic AI-SaaS dashboard
(purple gradients, glassmorphism cards, icon-sidebar-plus-stat-tiles). That
constraint should shape the *choice*, not just be a style filter applied
after the fact: the strongest direction is one where the UI's visual identity
and the product's actual substance (double-entry bookkeeping, trustworthy
history, nothing ever silently mutated) point the same way.

## Research findings (reference points)

- **Ramp** — black-and-white editorial system with a single highlighter-yellow
  accent used only where money moves (CTAs, live counters, active states);
  single-weight neo-grotesque type; "bento box" product-graphic grids.
- **Mercury** — near-monochrome (#1B1B1F + off-white + grays), product
  screenshots treated as editorial photography rather than UI chrome — reads
  closer to a luxury-goods brand than a bank.
- **Robinhood** (2023 rebrand) — custom sans ("Phonic") paired with a serif
  ("Martina Plantijn") for headlines: an explicit serif+sans pairing in a
  finance-trust context, rather than the usual all-sans SaaS look.
- **Wise** (2023–24 brand refresh / "Editorial Design System") — the source
  material explicitly names the problem this research is trying to avoid:
  fintech converging on "a sea of sameness," and describes moving toward
  illustration, distinct iconography, and a multi-brand token system as the
  fix.
- **Bloomberg Terminal** — the standing reference for information density as
  a deliberate, legitimate design stance: no whitespace, everything visible
  at once, "shows you everything and trusts you to figure it out." Relevant
  because this product's whole point is showing its own correctness rather
  than hiding structure behind a simplified consumer gloss.
- **`ledger-cli`** (plain-text, CLI-only double-entry accounting tool) —
  confirms "terminal-native ledger" is a real, existing pattern in the
  accounting-software space, not an aesthetic borrowed wholesale from trading
  platforms.
- **Command palette (Cmd/Ctrl-K)** — an established *supplement* to visible
  navigation, not a full replacement for it; pairs with a thin structural nav
  for discoverability.
- **Typography for financial numerals** — consensus from type-design sources:
  `font-variant-numeric: tabular-nums` on a proportional sans usually beats
  switching the whole UI to monospace; monospace is best used selectively
  (hero figures, transaction IDs, timestamps), not globally. Numeral glyph
  shapes should avoid 1/I and 0/O ambiguity.
- **WCAG for financial UI** — 4.5:1 minimum text contrast; state (approved /
  pending / reversed) must never be color-only — pair with icon + text label,
  called out specifically for status badges and transaction states.
- **Brutalism-in-finance risk** — multiple sources warn that raw/undisciplined
  brutalist UI reads as untrustworthy for money products *unless* the
  starkness clearly signals competence rather than neglect (the
  Bloomberg/`ledger-cli` precedents show austere ≠ untrustworthy when it's
  deliberate).

## Three directions

### 1. Ledger-as-Instrument — data-dense, terminal-adjacent, trust-through-transparency

**Visual philosophy.** The UI doesn't hide the double-entry model behind a
simplified "consumer fintech" gloss — it shows it. Debits, credits, and
running balances are visible on the main dashboard, not tucked behind a
"view details" tap. Correctness is the product; the UI's job is to make that
legible, the way `ledger-cli` and a trading terminal make their data legible
by refusing to compress it.

- **Navigation model.** A thin, always-visible structural nav (not an icon
  sidebar) plus a Cmd/Ctrl-K command palette as the primary way power users
  move around (jump to a wallet, start a transfer, search a transaction).
  The palette supplements the nav; it doesn't replace it — first-time users
  still see labeled destinations.
- **Information density.** High. The dashboard is a ledger view first: a
  computed balance header above a real entry table (date, direction,
  counterparty, amount, running balance), not an isolated "stat tile" divorced
  from the data that produced it.
- **Typography.** Proportional UI sans for labels/body; `tabular-nums` for
  all amounts (not full monospace UI); true monospace reserved for
  transaction IDs, idempotency keys, and timestamps — where it signals "this
  is a precise machine-verifiable value," not decoration.
- **Component language.** Tables and rules, not cards and shadows. Borders
  over drop-shadows. Status conveyed via icon + text label + color together
  (never color alone), per the WCAG finding above.
- **Motion philosophy.** Near-none. Values update instantly or via a brief
  numeric tick on change. No card transitions, no parallax, no decorative
  easing. Motion is only ever a signal that something changed — never
  ambience.
- **Dashboard approach.** Single scrollable ledger-first view. Balance is a
  computed header derived from visible entries below it, reinforcing (not
  just asserting) that the number is trustworthy.
- **Mobile approach.** The entry table becomes a stacked list at ~320px but
  keeps debit/credit labeling explicit — information is re-flowed, never
  dropped, at small widths.
- **Accessibility notes.** Strong by default (high-contrast, text-first), but
  dense tables need real `<table>`/ARIA semantics for screen readers and a
  skip-to-content link given how much is on one screen.
- **Differentiator vs. generic SaaS.** Directly opposite instinct from
  card-grid dashboards: shows the mechanism instead of abstracting it away.
  For a project whose entire pitch is "the ledger is the source of truth,"
  this is the direction where the UI argument and the engineering argument
  are the same argument.

### 2. Editorial Calm — restrained monochrome + single accent (Mercury/Ramp/Robinhood-derived)

**Visual philosophy.** Confidence through restraint and typographic quality
rather than decoration. One hero number (current balance) given real
typographic weight; everything else recedes.

- **Navigation model.** Simple top nav or a slim left rail; few destinations
  (this product doesn't need twenty nav items — wallets, transfers, activity,
  done). Generous whitespace.
- **Information density.** Low-to-medium. One prominent balance, supporting
  data in a calm secondary hierarchy below it.
- **Typography.** A serif or distinctive display face for the balance/
  headline figures (Robinhood-style serif+sans pairing), a clean grotesk for
  body/labels/nav — explicitly not "system sans everywhere."
- **Component language.** Near-monochrome (off-white / near-black), a single
  accent color used *only* on actionable/state elements (Ramp's
  "yellow-only-where-money-moves" rule) — never a decorative gradient.
- **Motion philosophy.** Subtle, restrained cross-fades and reveals. Calm,
  not bouncy — motion functions as a trust signal ("nothing sudden happens
  to your money"), not as delight-for-its-own-sake.
- **Dashboard approach.** Editorial framing of the balance (generous margin,
  careful crop/composition if any imagery is used at all), a plain, legible
  transaction list beneath it.
- **Mobile approach.** Single column, generous padding that compresses
  gracefully; the accent color's usage stays disciplined even at small sizes
  (no accent-everywhere at narrow widths).
- **Accessibility notes.** The monochrome-plus-one-accent palette needs the
  accent contrast-checked against both a light and a dark surface; status
  still needs icon+label, not color alone.
- **Differentiator vs. generic SaaS.** The explicit anti-gradient,
  anti-glassmorphism direction — but it's the direction with the most
  execution risk: without a genuinely distinctive type or accent choice it
  can drift into "another Stripe/Mercury clone," which is itself a
  now-recognizable fintech-SaaS look.

### 3. Structural / Plain-Spoken — utility-grade, honest-by-construction

*Most speculative of the three — synthesized from accessibility literature
and the brutalism-risk warning above rather than a named fintech case study;
flagged here rather than presented as a validated pattern.*

**Visual philosophy.** Rejects "fintech-as-lifestyle-brand" outright. Reads
like a well-built public utility or tax-software product — credible because
it's plain, not because it's polished.

- **Navigation model.** Plain labeled text links/tabs, no icon-only nav —
  everything spelled out in words.
- **Information density.** Medium, organized as clearly labeled sections/
  forms rather than dashboard widgets.
- **Typography.** One highly legible, slightly unusual workhorse sans or
  slab at a restrained size scale; no display/decorative face.
- **Component language.** Flat, bordered, no shadows, no rounded-card motif.
  Buttons and inputs look like form controls, not marketing surfaces.
- **Motion philosophy.** Functional only — focus rings, state changes.
  Deliberately unpolished in the "flashy" sense, polished in the "fast and
  works everywhere" sense.
- **Dashboard approach.** Numbers shown in context with their derivation
  inline, rather than isolated stat tiles — the ledger-correctness story told
  through plainness instead of Direction 1's density.
- **Mobile approach.** Trivial to reflow — it's already table/form-based
  rather than widget-based.
- **Accessibility notes.** Strongest of the three by construction; this
  direction and WCAG compliance are nearly the same goal.
- **Differentiator vs. generic SaaS.** Genuinely uncommon portfolio choice —
  "credible because boring" in a space saturated with polish. Real risk:
  can under-sell the engineering work if it reads as *merely* plain rather
  than deliberately so.

## Decision

Per the assigning session's instruction not to block Task 2 on a reply: no
response arrived by the time this document and the research behind it were
the only remaining blocker, so this session is picking.

**Chosen: Direction 1 — Ledger-as-Instrument.**

Rationale: the project's own stated centerpiece is that "the ledger is the
source of truth" and balances are *derived*, never stored — Direction 1 is
the only one of the three where the visual design directly demonstrates that
claim (visible entries, visible running balance, visible debit/credit
structure) rather than merely avoiding generic patterns while asserting
trustworthiness through restraint (Direction 2) or plainness (Direction 3).
It also has the strongest, most concrete precedent (Bloomberg Terminal,
`ledger-cli`) of the three, and the least execution risk of drifting into a
"clone" of an existing product, which Direction 2 was flagged as risking.

This is a call, not a locked decision — if the other session responds with a
different preference before frontend work is far along, it's cheap to revisit
the token/typography layer and expensive to revisit page structure, so raise
it sooner rather than later.

M5 frontend implementation proceeds against Direction 1.
