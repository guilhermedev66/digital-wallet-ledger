# M8.1 Visual Reference Board

Research pass before the M8.1 structural redesign. Firecrawl's MCP key was
invalid this session (revoked), so this research ran through `WebFetch`
against the three sites requested. Palette stays put — near-black, graphite,
charcoal, warm gray, off-white, amber/orange accent — this board is about
**composition, hierarchy, and interaction**, not new brand colors.

## Checkpoint

**REFERENCES FOUND:**
- Inspora ([inspora.design](https://www.inspora.design/)): thin on full
  fintech case studies (it's a rolling inspiration feed, not a curated
  fintech gallery), but two posts are directly usable — see below.
- Refero ([styles.refero.design](https://styles.refero.design/)): strong hit.
  AI-readable `DESIGN.md` extracts of real product marketing sites, four of
  which are fintech or fintech-adjacent (Wise, Ramp, Brex) plus Linear for
  the dark "command center" app-shell language this project already leans on.
- Spell ([spell.sh](https://spell.sh/)): a small free React component
  library (`spell-ui`, MIT, github.com/xxtomm/spell-ui). Useful for a
  handful of concrete, non-gimmicky micro-interactions.

**DESIGN ELEMENTS SELECTED:**
- App shell → Linear (hairline dark surfaces, single-accent nav)
- Dashboard → Ramp (flat metric cards, live-counter band) + Inspora
  "Interactive Cards" (dark + orange data card treatment)
- Wallet card → Inspora "Interactive Cards" + Wise (currency pill/flag chip)
- Transfer → Wise (pill segmented control) + Inspora "Payment Links" (stacked
  connection composition) + Spell Pop Button (confirm interaction)
- Activity → Ramp (hairline-bordered flat list, ticker-style running totals)
- Reconciliation → Brex ("precision instrument for finance teams" single-accent
  status language) + Spell Badge
- Motion → Spell (Pop Button, Copy Button, Label Input floating-label pattern)

---

## Curated References

### 1. Linear — app shell / navigation
**SOURCE:** Refero — `styles.refero.design/style/90ce5883-bb24-4466-93f7-801cd617b0d1`
**REFERENCE:** Linear marketing-site `DESIGN.md` extract
**SCREEN/PATTERN:** Dark command-center surface system: `#08090a` canvas →
`#0f1011` card → `#161718` elevated, separated by 0.5–1px hairline borders
(`#23252a`) instead of shadows; one chromatic accent reserved for the single
primary action per view; nav text buttons at 13px, ghost/outline buttons with
1px border.
**WHY IT IS GOOD:** This is the closest documented match to what M8/M8.1
already committed to (near-black + graphite + single accent), but with a
disciplined rule set we haven't enforced yet: hairline borders over shadows,
one accent per view, no bold weights above 510.
**WHAT EXACTLY WE WILL ADAPT:** The three-tier surface elevation
(canvas/card/elevated via hairline borders, not box-shadow) for `AppShell`
and page containers; the "one accent per view" discipline for primary CTAs;
the nav text-button sizing (13px, 8×12 padding) for the top nav / tab bar.

### 2. Ramp — dashboard metrics & activity list
**SOURCE:** Refero — `styles.refero.design/style/b38702a0-75ab-474c-9106-00b624535825`
**REFERENCE:** Ramp marketing-site `DESIGN.md` extract
**SCREEN/PATTERN:** Flat "wash" cards (`#f4f2f0` fill, no border) vs. content
cards (`#ffffff`, 1px `#e5e7eb` border) — elevation communicated by fill
color, never shadow. Full-width dark ticker band (`#1a1919`) with uppercase
10px labels and white 14px metric values for live counters.
**WHY IT IS GOOD:** Ramp is a corporate-card/expense fintech — its "wash vs.
content card" distinction maps directly onto our need to separate
"decorative/grouping" surfaces from "here's a real number" surfaces on the
Dashboard, without inventing a new elevation language.
**WHAT EXACTLY WE WILL ADAPT:** The wash/content card split for Dashboard
sections (wallet summary strip = wash, individual wallet balances = content
cards); the ticker-band treatment for a slim "total balance across
currencies" strip at the top of the Dashboard (real data only — no fake FX).

### 3. Brex — reconciliation / status language
**SOURCE:** Refero — `styles.refero.design/style/b58d92f6-68a8-4358-8fc9-6ea58e6d483b`
**REFERENCE:** Brex marketing-site `DESIGN.md` extract
**SCREEN/PATTERN:** "Designed to feel like a precision instrument for
finance teams, not a marketing site" — flat components, minimal shadows,
hairline borders, exactly one chromatic accent (`#ff5900`, an ember/orange
already close to our own accent family) used only for the single primary
action.
**WHY IT IS GOOD:** Reconciliation is explicitly the screen the user wants
as a visual differentiator — "a user should immediately understand whether
the ledger is healthy." Brex's restraint (one accent, flat surfaces, no
noise) is exactly the register for a health/status screen: nothing should
compete with the health signal itself.
**WHAT EXACTLY WE WILL ADAPT:** Single-accent status treatment — a clean
"healthy" state stays entirely neutral (graphite/off-white, no color), and
the accent color is reserved *only* for when something needs attention
(unbalanced transactions). This inverts the usual "green = good" pattern
into "silence = good, accent = look here," which reads as more deliberate.

### 4. Wise — currency selector / segmented control
**SOURCE:** Refero — `styles.refero.design/style/367c0c6e-73a7-441c-a8ff-91d139ac60dc`
**REFERENCE:** Wise marketing-site `DESIGN.md` extract
**SCREEN/PATTERN:** Pill-shaped segmented tabs (9999px radius) with an
active-fill state; "currency selector pill" combining a circular flag chip
with country/currency name and an outlined "Change" affordance; dark
section card (28px radius, 40px padding) used to spotlight a single
grouped chunk of content.
**WHY IT IS GOOD:** This project already deals with 7 currencies across
wallets — Wise is the one reference here whose entire product is
multi-currency movement, so its currency-selector chip is a directly
transferable pattern rather than an analogy.
**WHAT EXACTLY WE WILL ADAPT:** The pill segmented control for
currency/wallet switching (Dashboard wallet tabs, Transfer's source-wallet
picker); a currency chip treatment (currency code + colored dot instead of
a flag, since this is a simulated wallet with no real countries) for wallet
cards and the transfer composition.

### 5. Inspora — "Interactive Cards" (dark + orange data cards)
**SOURCE:** Inspora — `inspora.design/posts/interactive-cards`
**REFERENCE:** "Interactive Cards" post
**SCREEN/PATTERN:** Dark-background metric cards with cream/orange accents,
minimal layout prioritizing the number over decoration, subtle
hover-responsive elevation.
**WHY IT IS GOOD:** It's the one Inspora hit that already sits in our exact
palette family (dark + orange), and it's a card pattern, which is precisely
the "generic bordered rectangle" the redesign brief singles out as
unacceptable for wallets today.
**WHAT EXACTLY WE WILL ADAPT:** The dark-card / orange-accent / number-first
hierarchy for the Wallet card component. We are explicitly **not** adapting
the literal 3D tilt/hover-glow animation the source uses — that reads as a
gimmick, which the brief rules out; we keep a subtle hover elevation (a
hairline border brightening, à la Linear) instead of a tilt effect.

### 6. Inspora — "Payment Links" (stacked connection composition)
**SOURCE:** Inspora — `inspora.design/posts/payment-links`
**REFERENCE:** "Payment Links" post
**SCREEN/PATTERN:** A modal built around a vertical stack that visually
connects "who/where this is going" to the action that sends it, using
connected-avatar/entity motifs above a clear single action.
**WHY IT IS GOOD:** The Transfer flow's biggest problem today is that
source wallet, amount, destination, and confirm are just stacked form
fields with no visual sense of *movement*. This pattern's core idea — make
the connection between two entities visible before the action — is the
right shape for "source → amount → destination → review → confirm," even
though the source domain (social payment sharing) differs.
**WHAT EXACTLY WE WILL ADAPT:** A vertical connector (not a literal avatar
chain — we don't have user avatars) linking the source-wallet chip to the
destination-wallet chip, with the amount as the visual focal point between
them, before the review/confirm step. Idempotency key stays a small
monospace caption below the fold, never competing with this composition.

### 7. Spell — Pop Button
**SOURCE:** Spell — `spell.sh/docs/pop-button`
**REFERENCE:** Pop Button component (`spell-ui`, MIT)
**SCREEN/PATTERN:** A button with a push-down 3D animation on press —
functional tactile feedback, not decorative motion.
**WHY IT IS GOOD:** Money-movement actions (confirm transfer, fund, reverse)
deserve a moment of deliberate, felt confirmation — a small physical "this
just happened" — without adding a gimmick like confetti or particles.
**WHAT EXACTLY WE WILL ADAPT:** The press-down interaction (translateY +
shadow compression on `:active`, ~80–120ms) for the Transfer confirm button
and the wallet funding/reversal action buttons only — not for routine
navigation or secondary buttons, to keep it meaningful.
**IMPLEMENTATION LOCATION:** `frontend/src/components/Button.tsx` (new
`variant="commit"` or a CSS-only `:active` treatment scoped to those call
sites).

### 8. Spell — Copy Button
**SOURCE:** Spell — `spell.sh/docs/copy-button`
**REFERENCE:** Copy Button component (`spell-ui`, MIT)
**SCREEN/PATTERN:** Copy-to-clipboard button with a blur/fade transition on
the icon swap (copy → check).
**WHY IT IS GOOD:** Wallet IDs, transaction IDs, and idempotency keys are
technical strings users need to copy but that shouldn't visually compete
with the primary content (explicitly called out in the brief).
**WHAT EXACTLY WE WILL ADAPT:** The icon-swap-with-blur affordance for
copying a wallet ID / transaction ID, keeping those IDs small, monospace,
and low-contrast until hovered/focused.
**IMPLEMENTATION LOCATION:** New small `CopyableId` component used in
`WalletCard`, `ActivityPage` transaction rows, and the Transfer
idempotency-key caption.

### 9. Spell — Label Input
**SOURCE:** Spell — `spell.sh/docs/label-input`
**REFERENCE:** Label Input component (`spell-ui`, MIT)
**SCREEN/PATTERN:** Floating-label input pattern.
**WHY IT IS GOOD:** The amount input in Transfer/Fund flows is the single
most important field in the whole app; a floating label that collapses into
a caption on focus keeps the field visually dominant while still labeled.
**WHAT EXACTLY WE WILL ADAPT:** Floating-label treatment for the amount
input specifically (large numeric display, currency code as a fixed
affix, label collapses above on focus/value) — not applied wholesale to
every form field, to avoid over-using the pattern.
**IMPLEMENTATION LOCATION:** `frontend/src/components/Field.tsx` (new
`AmountField` variant) used in Transfer and the wallet funding form.

### 10. Spell — Badge
**SOURCE:** Spell — `spell.sh/docs/badge`
**REFERENCE:** Badge component (`spell-ui`, MIT)
**SCREEN/PATTERN:** Badge with multiple color variants/sizes for compact
status labeling.
**WHY IT IS GOOD:** Reconciliation health, transaction type (Transfer /
Funding / Reversal), and entry direction (Debit/Credit) all need a small,
consistent status-label primitive instead of ad hoc colored text.
**WHAT EXACTLY WE WILL ADAPT:** A single `Badge` component (neutral by
default, accent only for "needs attention") reused across Reconciliation
status, Activity transaction-type tags, and Dashboard reconciliation-health
summary — consistent with the Brex "silence = good, accent = look here"
principle from reference #3.
**IMPLEMENTATION LOCATION:** New `frontend/src/components/Badge.tsx`.

---

## Design Recipe

**APP SHELL**
→ Linear (#1): three-tier hairline elevation, one accent per view, 13px nav
text buttons.

**DASHBOARD**
→ Ramp (#2) for wash/content card split and the live-total ticker band,
combined with Inspora Interactive Cards (#5) for the wallet-summary tiles'
dark+orange number-first treatment. Real data only — no fake FX/analytics.

**WALLET CARD**
→ Inspora Interactive Cards (#5) for the dark/orange, number-first card
body; Wise (#4) for the currency chip (code + dot, no flags).

**TRANSFER COMPOSITION**
→ Wise (#4) pill segmented control for source-wallet selection; Inspora
Payment Links (#6) for the vertical source→amount→destination connector;
Spell Pop Button (#7) for the confirm action; idempotency key demoted to a
small monospace caption (Spell Copy Button, #8).

**ACTIVITY**
→ Ramp (#2) flat hairline-bordered row list; Spell Badge (#10) for
transaction-type tags; Spell Copy Button (#8) for transaction IDs.

**RECONCILIATION**
→ Brex (#3) single-accent "precision instrument" status language; Spell
Badge (#10) for the health indicator, styled neutral-by-default per the
"silence = good" principle.

**MOTION / MICRO-INTERACTION**
→ Spell Pop Button (#7) on commit actions only (transfer confirm, fund,
reverse); Spell Copy Button (#8) on all copyable IDs; Spell Label Input
(#9) on the amount field only.

---

## What we are explicitly not doing

- Not copying any of these products' full page layouts wholesale.
- Not introducing new brand colors — palette stays near-black / graphite /
  charcoal / warm-gray / off-white / amber-orange.
- Not adopting Inspora's literal 3D tilt/particle effects (Interactive
  Cards' hover-glow, Spell's Exploding Input/Tilt Card/Perspective Book) —
  those read as gimmicks and are out of scope per the brief.
- Not touching backend/domain logic — this is presentation-layer only.
