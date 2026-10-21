# The road — the Longmother and all ten `campaign.json` themes

There is no overworld (`docs/BIBLE.md` §28, reaffirmed GG-T115) — the road is told through each theme's
`Crawl` text and its dialogue, not walked on a map. This file treats the Longmother herself as the zeroth
location (everywhere is, literally, standing on her) and then the five main themes plus their five
postgame counterparts, in campaign order. Every "Bio" below condenses the shipped `Crawl` in
`campaign.json`; nothing here contradicts it, and a writer should read the full crawl before writing a
scene set in that theme, not just this summary.

## The Longmother — the ground everything else sits on

**Bio:** An ageless spider-colossus nobody remembers hatching. The town is built on her back, nailed
down to her own old carapace plate. She has never once stood still long enough for anyone to ask how
long this has been going on, because standing still is what kills — ground she stops on sours into a
blight that fuses the living with the grown-and-built. She can't recross ground she's already woken, so
her route is a long curl rather than a straight line, carrying her back through the same *kinds* of
country without ever retreading the same ground twice — which is why the five postgame themes exist at
all (`docs/BIBLE.md` §27.1).

**Voice:** She never speaks — no dialogue node in `dialogues.json` is attributed to her, and none should
be. She is weather, not a character: felt in footfalls, in how every crawl orients itself around her
motion, in the caravan's gear ("grown from her own molted plate, not mined") and its pincer tactics
("copied from watching her brood hunt"). If a future writer ever wants her to speak, that's a bigger
call than this seed document should make alone — see `docs/design-draft-overworld-and-dialogue.md`'s own
open questions about naming her and the plague she's outrunning.

**Needs advice about:** Nothing anyone on her back could possibly offer, and that's the point. She is the
one presence in this universe who is definitionally past taking advice — every hero, enemy and NPC in
this folder is instead giving each other advice about *her* rule (keep moving, don't stand still, don't
look back) without ever being able to address it to the one who set the rule in the first place.

---

## Green Valley

**Stages:** `greenvalley-01/02/03`. **Joins here:** Merchant (I), Innkeeper (after I).

**Bio:** The last green ground on the old road, and already failing — tainted wells, frogs abandoning
the marsh edge for dry land. Not an invasion; the crawl insists on this directly: "this isn't an attack.
It's the first proof that staying was never actually safe here — it just hadn't caught up yet." Every
footfall cracks open centuries of undisturbed ground and whatever's grown down there. Magic has begun to
flicker here, which is the whole reason the caravan is already leaning on what can be built and burned.

**Voice:** Elegiac but unsentimental — mourns the valley's last green stretch without romanticizing it; a
place realizing, in real time, that its safety was always borrowed.

**Needs advice about:** Letting go of the idea that staying put was ever actually safe. Every family and
NPC met here (Slime, Wolf, Ganger, the Merchant, the Innkeeper) is working through some version of this
same lesson at a different pace — the valley itself is the first and gentlest classroom on the road.

## Sandsea Reaches

**Stages:** `desert-01/02/03`. **Joins here:** Alchemist (I).

**Bio:** Ground the Longmother has crossed before — old wagon ruts that aren't hers, goblin clans' buried
works turning back up under her feet. Heat and thirst are the obvious danger; the real one is what the
crew pulls from the sand between fights: the first clean proof the blight has a *shape* — joints that
shouldn't bend, plating that grew instead of forged. Spellcraft frays worse here than in the valley.

**Voice:** Harsher, drier, more clinical than Green Valley — a place that deals in evidence (what gets dug
up) rather than atmosphere.

**Needs advice about:** Accepting a clean answer isn't coming. The Reaches is the theme that forces the
caravan to stop treating the blight as weather and start treating it as a designed thing — advice here
is less emotional, more diagnostic, which is why the Alchemist (unsentimental, candid about not knowing
what's in her own flasks) is exactly who joins at this theme's start.

## Mireholt Fens

**Stages:** `swamp-01/02/03`. **Joins here:** Enchantress (I).

**Bio:** Wet, low ground where the blight pools instead of drying off — "every fen is worse than the
last fen." Hags and swamp-things aren't passing through; they've made a rotting home of exactly the
ground the Longmother has to cross, and resent her for it. This is also where the first working magic
reliable enough to survive the interference was found — runes bound into steel rather than cast loose.

**Voice:** Heavier, wetter, more resentful than the Reaches — a place that's stopped being merely
dangerous and started being *hostile*, on purpose, from creatures with an actual stake in the ground.

**Needs advice about:** Whether defending ground that's already dying is grief or denial. Nearly every
enemy family here (the Hag, the Swamp Mistress, the Naga) is some version of "still ruling/warding/lurking
ground that's already lost," and the Enchantress's own arrival — solving the magic-fraying problem by
binding workings into steel instead of hoping — is the theme's answer in miniature: adapt the method
before the ground is gone, don't defend the method past its use.

## Frostmaw Caverns

**Stages:** `cave-01/02/03`. **Joins here:** Blacksmith (already joined, Green Valley II — no new NPC).

**Bio:** The road climbs out of the fens into cold caverns where, for once, the danger wasn't stirred up
by the Longmother's own feet — it was already waiting. Cold slows the blight the way it slows everything
else, which means Frostmaw holds the oldest, least-disturbed things on the whole route. Here the crew
finds the first thing unmistakably *built*, not merely grown or buried — proof something made the blight
on purpose, a long time before the Longmother ever walked this close.

**Voice:** Slower, colder, more deliberate than anywhere prior — a place where time itself moves
differently, which the crawl treats as genuinely sinister rather than merely atmospheric.

**Needs advice about:** Recognizing intent. Every theme before this one treated the blight as something
that happened; Frostmaw is where the caravan has to start asking *who* did it happen on purpose for — the
turning point the crawl names outright. The Ice Mauler, oddly, is the one voice in this theme already
answering that question in miniature: magic still works cleanly here, and it should be telling the
caravan something.

## Veshker Ruins

**Stages:** `cityruins-01/02/03`. **Joins here:** no new service; this theme is the campaign's climax.

**Bio:** A dead city — stone instead of carapace, built by people who tried to do what the Longmother
does without ever learning to walk. They dug in, raised walls, told themselves walls built well enough
were the same as never standing still, and failed the way every grounded thing on this road eventually
fails. Except here something never let the failure finish: the Vampire Lord, the city's former ruler,
found a way to keep "stopped" going past when it should have killed him. He wants the Longmother to stop
too, so he can finally be still on ground that won't rot under him. This is the climax of the road so
far — not another step ahead of something chasing, but a flat refusal to repeat the city's own mistake.

**Voice:** Grand, funereal, the most overtly gothic theme in the campaign — a dead civilization's pride
still audible in its ruins, and a boss who genuinely believes stopping is the answer rather than the
disease.

**Needs advice about:** The entire campaign's thesis, stated as plainly as the bible ever states anything:
walls built well enough are not the same as never standing still. The Vampire Lord needs this advice more
than anyone else in the game and is the one character guaranteed never to take it (he's a hero class too,
`heroes.md`'s Vampire — same unresolved question, no matter which side of the fight he's on).

---

## The five postgame themes — the road continuing past an open question

Per `docs/BIBLE.md` §27.1: the Vampire Lord's fall "didn't end the march, and it didn't cure anything —
the blight doesn't go anywhere just because its best host finally did." Because the Longmother can't
recross ground she's already woken, her route curls back through the same *kinds* of country rather than
backtracking — these five themes are that curl, each one revisiting its main theme's *kind* of ground,
never the same ground.

### Green Valley, Deeper In

**Stages:** `greenvalley2-01/02`.

**Bio:** The curl closing on green country again, deeper than the old maps ever reached. It looks like
home. It isn't — these wells never had travelers to warn them, and whatever's working this ground has
had longer, undisturbed, to get good at it.

**Voice:** Uncanny-familiar — the valley's own warmth curdled by the fact that nothing here has ever been
warned, unlike the original Green Valley.

**Needs advice about:** That familiarity is not safety. This theme exists specifically to undercut
whatever comfort the original Green Valley earned — advice here should actively distrust anything that
looks like home.

### Sandsea Reaches, Further Out

**Stages:** `desert2-01/02`.

**Bio:** Further than the dunes already crossed, where half-grown, half-built things stop being a rare,
unsettling find and become common country. The clearest evidence yet that a "Demon Lord" isn't one throne
— it's a shape the blight keeps finding wherever a host is strong enough to hold it.

**Voice:** Matter-of-fact about horror in a way the original Reaches wasn't yet — this theme has stopped
being surprised.

**Needs advice about:** That naming a thing (Demon Lord) as a single villain is a comfort, not an answer —
the real lesson here is structural: the blight doesn't need a particular body, it needs only a strong
enough one.

### Mireholt Fens, Past the Mire

**Stages:** `swamp2-01/02`.

**Bio:** Past the mire already fought through, the fen only gets wetter, the blight only settles deeper.
The crew isn't the only vanguard working this ground — wreckage further in carries markings that aren't
the caravan's own, left by people "either still down here or didn't get the chance to leave." Nobody
comes this far out who still has somewhere better to be.

**Voice:** The loneliest theme in the game — isolation as the dominant mood, more than danger.

**Needs advice about:** That the caravan is not unique in this fight, and not uniquely lucky either — the
unnamed other vanguard's wreckage is a warning the party should actually sit with, not just salvage past.

### Frostmaw Caverns, Lower Depths

**Stages:** `cave2-01/02`.

**Bio:** Lower than the veins the caravan has mined for a generation, following the same seams toward
wherever the first built thing was built. The mana ore thins and strangles the deeper the crew follows
it — half-crystal, half-something-else, less of it burning clean. The choice between spending the little
reliable fuel left or trusting a casting in ground this far gone has never felt sharper; most of the crew
no longer trusts the casting.

**Voice:** Claustrophobic, resource-anxious — the theme most explicitly about running out, of fuel and of
options.

**Needs advice about:** Whether the magic-vs-machine tension (`docs/BIBLE.md` §27.1, still open as a
mechanical system per `docs/design-draft-overworld-and-dialogue.md` §6.3) has actually reached its limit
here, or just feels like it has because the crew is tired. This is the theme most likely to host a
scene where a hero's choice of ability (spell vs. skill) becomes a character beat, not just a tactic.

### Veshker Ruins, Inner Wards

**Stages:** `cityruins2-01/02`.

**Bio:** Past the wards the Vampire Lord held, deeper into Veshker than his fall ever reached — older
construction under the newer, and older still under that. Whoever built the city's failed foundations
wasn't the first to try keeping ground still here, either. The road doesn't end at a dead king's throne;
it goes on being the only question the caravan has ever actually chased: not how to beat what's down
here, but what, at the bottom of all of it, the blight actually *is*. Nobody has an answer yet.

**Voice:** The most unresolved theme on purpose — deliberately anticlimactic after the Vampire Lord,
because the real antagonist was never a person.

**Needs advice about:** Accepting an ending that doesn't resolve. This is the frontier of the shipped
story as of 2026-10-20 (`docs/BIBLE.md` §27.1's own open design questions, §29) — any future writing set
here should lean into "no cure, no final answer" rather than quietly supplying one; that restraint is
itself the design intent per `docs/design-draft-overworld-and-dialogue.md`'s own open questions.
