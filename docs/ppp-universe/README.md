# The PPP Prose Universe — content bible seed (GG-T137)

**PPP** is the owner's working name for a Prose universe built around this game's world and cast, "so
believable stories and dialog exchanges can be had." This folder is the **seed**: prose source material
written against the content that already exists in `classes.json`, `campaign.json` and `dialogues.json`,
not a new design. It establishes nothing BIBLE.md doesn't already allow for; it just gives every named
thing in the roster a bio, a voice, and an angle a writer can turn into a scene.

It is **not** itself game content. Nothing here is loaded, validated or read by the game. It exists to
be mined by:

- **GG-T133** — a Talk dialogue per hero class where picking the choice that matches that hero's
  established personality/advice-need grants a skill point. Every hero entry below ends in a line
  written for exactly that purpose.
- **GG-T120's party conversations** — the `*_party_talk` beats already shipped per theme
  (`docs/BIBLE.md` §27.1) are the model; this seed seeds the *next* round of those, plus any future
  Camp Talk or theme-start dialogue.
- **A later authoring pass in the Prose repo**, once that repo's shape for a game-tie-in universe is
  confirmed. Until then this lives here, in Unity.GridGame, next to the content it describes.

## What's established vs. what's invented here

Every fact below is grounded in shipped content: `Assets/Gaiden/Resources/Content/classes.json`
(`Role`, `HomeTree`, `DefaultLoadout`, `Tags`), `campaign.json` (theme `Crawl` text), `dialogues.json`
(the Merchant/Innkeeper/Blacksmith/Alchemist/Enchantress join scenes and Camp chats already written for
GG-T117–120), and `docs/BIBLE.md` §13–§25 (roles, sub-archetype clusters, the Longmother/blight/Vampire
Lord story). The voice and bio text itself is new prose written for this ticket; it does not override
or amend any of that — if a later writer's instinct disagrees with a line here, the shipped dialogue and
the bible win, not this seed.

## The one frame everything else sits inside

The town walks on the Longmother's back because standing ground sours into a blight that fuses the
living with the grown-and-built; she can't recross ground she's already woken, so the road curls rather
than backtracks (`docs/BIBLE.md` §27.1, §28). The party are vanguard crews who drop at each footfall to
clear what the step disturbed and salvage what it turned up. Magic frays wherever the blight has
spread; machines don't care. The Vampire Lord — Veshker Ruins' climax — is the one being on the road
who found a way to stay stopped instead of outrunning it, and wants the Longmother to join him. Nobody
has an answer yet for what the blight actually *is*; the five postgame themes are the road continuing
past that open question, deliberately uncured (`docs/design-draft-overworld-and-dialogue.md` §1).
Every bio in this folder is written to sit inside that frame, not around it.

## Files

| File | Covers | Count |
|---|---|---|
| [`heroes.md`](heroes.md) | Every `Team.Hero` class, grouped by Role then by the sub-archetype cluster its `DefaultLoadout` actually walks (`docs/BIBLE.md` §25.6.1/§25.13) | 64 |
| [`enemies.md`](enemies.md) | Every enemy *family* (the 00/01/02… tiers of one name share one voice, the way the Bestiary and drop tables already treat them), plus the four named/boss enemies who stand apart from their family | ~39 families + 4 named |
| [`camp-npcs.md`](camp-npcs.md) | The five Camp NPCs who join the caravan along the way (`docs/BIBLE.md` §25.0.1) | 5 |
| [`locations.md`](locations.md) | The Longmother's road itself, plus all ten `campaign.json` themes (five main, five postgame) | 1 + 10 |

## The template

Every entry carries the same three fields, because every entry is meant to answer the same question a
Talk-quiz or a party-talk beat eventually asks: *who is this, how do they talk, and what do they need to
hear?*

- **Bio** — a few sentences: who/what they are, inside the Longmother's-road frame above.
- **Voice** — diction and tone notes a writer can hold onto across many scenes, not just one line.
- **Needs advice about** — the thing this subject is wrong about, stuck on, or hasn't said out loud yet.
  For a **hero**, this is literally the Talk-quiz's right answer: the choice that names this correctly is
  the one that earns the point (GG-T133). For an **enemy**, it doubles as the reason a mid-battle
  recruitment hook (the still-open `docs/design-draft-overworld-and-dialogue.md` §4) might someday talk
  one of them down instead of killing it — most won't take the advice, which is exactly why they're still
  enemies. For a **location**, it's the question that theme's crawl and party-talk beat are already
  circling, named plainly for a future writer who wants to go further.

## Status

Seed only, written 2026-10-20 against content current as of that date (classes.json's 64/114 split,
campaign.json's 10 themes, dialogues.json's 25 beats). A future authoring pass — here or in the Prose
repo — should treat every line below as a draft a writer can cut, contradict or build on, not a locked
script.
