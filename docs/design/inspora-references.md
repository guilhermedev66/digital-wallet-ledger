# Inspora — composition references (M8.2)

Inspora is used here strictly as **visual/composition inspiration**, per its role in the
M8.2 brief. Nothing from Inspora is incorporated as code, asset, or literal layout — no
image, SVG, font file, or markup from these posts exists anywhere in this repository. What
follows is the specific composition *principle* selected from each post and which screen it
is meant to inform. Retrieved 2026-09-24 via live page fetch (not from memory).

## 1. Payment Links
**URL:** https://www.inspora.design/posts/payment-links
**What it shows:** A payment-link sharing modal — a vertical stack that visually connects
"who/where this is going" to the single action that sends it, using a connected-entity
motif above a clear primary action.
**Composition principle adapted:** Make the connection between two entities (source,
destination) visible *before* the action, rather than presenting them as two unrelated form
fields. This is the structural idea behind treating Transfer as a connected chain
(source wallet → amount → destination wallet → review → confirm) instead of a flat form.
**Applies to:** Transfer.

## 2. Interactive Cards
**URL:** https://www.inspora.design/posts/interactive-cards
**What it shows:** Dark-background metric cards with cream/orange accents, minimal layout
prioritizing the number over decoration, hover-responsive elevation (the source uses a
literal 3D tilt/glow on hover — noted below as **not** being adapted).
**Composition principle adapted:** Dark-card / single-accent / number-first hierarchy for a
data card — the balance is the dominant element, everything else is caption-weight
supporting text. The literal 3D tilt/hover-glow is explicitly excluded (gimmick, not
composition).
**Applies to:** Wallet card, Dashboard summary tiles.

## 3. Session Progress and Recovery Timeline
**URL:** https://www.inspora.design/posts/session-progress-and-recovery-timeline
**What it shows:** A dark-UI, orange-accent timeline visualizing progression through
multiple states (active → in-progress → completed/recovery), with clear visual
differentiation between state types along a single track.
**Composition principle adapted:** A single continuous track with per-state visual
differentiation, for showing a multi-stage process as one line of motion rather than
disconnected steps. Reinforces (does not replace) the Payment Links idea for Transfer's
step sequence, and is the closer reference specifically for *state* representation
(pending/posted/reversed) rather than entity-connection.
**Applies to:** Transfer (step states), potentially Activity (transaction state badges).

## Considered and rejected

**Tap Get Invoice** (`/posts/tap-get-invoice`) — a "paperfold" animation where a receipt
unfolds on tap. Visually strong (black/cream, high contrast) and thematically close to a
transaction receipt, but the core interaction *is* the literal fold/unfold gimmick — there's
no non-gimmick version of this pattern to extract. Excluded per the standing "no visual
gimmicks" rule (M8_1_VISUAL_REFERENCES.md's own exclusion list, still in force for M8.2).

## Full post list found (for record-keeping, not all relevant)

chat-component-interaction, onboarding, folder-icon-timeline, ticket-stub,
composer-mockup, folder-interaction, tap-get-invoice, dessn-welcome-envelope,
scenic-footer-section, payment-links, hypnotizing-ui, liquid-metal, enter-the-unknown,
session-progress-and-recovery-timeline, interactive-cards, holographic-card — all at
`https://www.inspora.design/posts/<slug>`. Only the three above were judged concretely
relevant to a fintech wallet/ledger app; the rest (chat UIs, folder metaphors, onboarding
envelopes, footers) don't map to this product's screens.
