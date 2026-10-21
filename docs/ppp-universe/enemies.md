# Enemies — all 39 `Team.Enemy` families (+ the Vampire Lord, who's a hero class too)

`classes.json` ships 114 enemy *classes*, but they're tiered variants of 39 named families — the same
unit the Bestiary tracks by name (`docs/BIBLE.md` §25.11: "Seen n/110") and the same unit each drop table
is keyed to (§24.7, one signature material per family). A `wolf00`/`wolf01`/`wolf02`/`wolf03` are one
character at four strengths, not four characters; this file gives that one character its bio, the way the
game already treats it as one bio in the Bestiary.

Grouped by the theme each family is first met in (cross-checked against every wave in `stages.json`, not
guessed from name) — which doubles as the family's home ground in `locations.md`. A few families recur in
a later theme; the bio below is written for where they're native, with a line for where else they turn
up.

**On "needs advice about" for an enemy:** this game has no permadeath and the design draft for mid-battle
recruitment (`docs/design-draft-overworld-and-dialogue.md` §4 — "survivors, deserters, or vanguard crews
from other caravans... the natural diegetic reason a stray unit stands on the grid asking to be convinced
rather than just another enemy") is still open, not built. So for every enemy below, "needs advice about"
is the thing that — if anyone ever got close enough to say it, and that unit were capable of hearing it —
might be the actual recruitment hook for that family. Most won't take the advice. That's exactly why most
of them are still enemies, not guests.

---

## Green Valley — the last green ground before the march shows what it really is

The valley's own crawl (`locations.md`) frames every creature here as proof that staying still was never
safe, not an invasion. Nothing in this list is malicious on arrival; the ground turned on them too.

- **Slime** (`slime00`–`slime03`) — Bio: the valley's softest warning sign, ooze that used to keep to
  the well bottoms and now doesn't. Voice (as a presence, not literal speech): formless, patient,
  reshapes around whatever hits it rather than resisting. Needs advice about: that spreading to fill
  whatever space is available isn't the same as actually going anywhere — it's the valley's blight made
  literal, and it has no destination, only drift.
- **Wolf** (`wolf00`–`wolf03`) — Bio: used to keep well clear of the road; packs now, where there used
  to be loners. Voice: communicates in pack logic — a lone wolf here is a wolf that got separated from a
  pack that's already compromised. Needs advice about: that the pack it's defending is sick the same way
  the wells are, and loyalty to a sick pack isn't the same as loyalty to what the pack used to be.
- **Bat** (`bat00`–`bat02`, recurs in Veshker Ruins) — Bio: nocturnal nuisance turned genuine threat as
  the dark under the valley's old plate gets more crowded. Voice: skittish, reactive, never the one that
  starts a fight. Needs advice about: that fleeing deeper into the dark (and by Veshker Ruins, into a
  dead city) isn't escape if the thing it's fleeing is airborne in the same tunnels it is.
- **Frog** (`frog00`–`frog03`, recurs in Mireholt Fens) — Bio: the crawl's own named proof of the
  blight's first move — frogs "that used to keep to the marsh edge... coming up onto dry land in numbers
  nobody's seen." Voice: chorus-like, rarely alone, loud before it's seen. Needs advice about: that the
  marsh it abandoned is where the Fens' own Swamp Mistress is trying to hold the line — going back might
  matter more than pushing further out.
- **Toad** (`toad00`, recurs in Mireholt Fens) — Bio: the Frog's slower, tougher cousin, the one that
  holds ground instead of hopping away from it. Voice: low, croaking, deliberate. Needs advice about:
  that holding ground is exactly the one thing nothing on this road can safely do (`locations.md`'s
  "standing ground goes bad") — it's doing the valley's one forbidden thing out of instinct, not malice.
- **Goblin Thug** (`goblinthug00`, recurs in Frostmaw Caverns) — Bio: not the organized clans Sandsea
  Reaches remembers, just muscle looking for something to guard or steal. Voice: blunt, transactional,
  asks "what's it worth" before "what's it for." Needs advice about: that there's nothing left to steal
  from ground this close to going bad — the thing worth guarding already left on the Longmother's back.
- **Ganger** (`ganger00`–`ganger06`) — Bio: Green Valley's own desperate humans, banded up once the wells
  turned and the town's law thinned out behind the caravan. Voice: nervous bravado, talks tougher than it
  fights. Needs advice about: that the caravan isn't the thing that wrecked the valley and isn't an
  enemy worth the risk — it's leaving anyway, on a schedule nobody here controls.
- **Soldier** (`soldier00`–`soldier03`, recurs in Sandsea Reaches) — Bio: what's left of whatever kept
  order here before the wells turned; still in uniform, increasingly unsure who the orders serve.
  Voice: clipped, procedural, falls back on rank and protocol. Needs advice about: that the chain of
  command it's still following has nobody left at the top of it.
- **Captain** (`captain`, recurs in Sandsea Reaches) — Bio: the Soldier's commanding officer, holding a
  post that stopped meaning anything the day the ground it guards started dying. Voice: authoritative,
  increasingly hollow under the authority. Needs advice about: that a post worth dying for and a post
  worth admitting is already lost are not always the same post, and it hasn't let itself tell which this
  one is.
- **Skelepede** (`skelepede00`–`skelepede02`, recurs in Frostmaw Caverns) — Bio: the valley's first
  unambiguous "metal fused to bone that shouldn't move and does" (the crawl's own phrase) — a centipede
  skeleton, armored, that was never alive in any one shape to begin with. Voice: clicking, numerous,
  no single voice so much as a chorus of segments. Needs advice about: that it was built from pieces of
  things that didn't consent to the arrangement, which makes it less a monster than a symptom — the
  advice, if any lands, is really for whatever's doing the building.
- **Undead** (`undead00`–`undead04`, recurs in Veshker Ruins) — Bio: the valley's own dead, risen off
  whatever ground the footfalls disturbed, before anyone built a proper grave for them. Voice: slow,
  repetitive, says the same half-sentence it died mid-saying. Needs advice about: that the thing it keeps
  trying to finish saying stopped mattering to anyone currently alive to hear it — rest is available, it
  just doesn't recognize the offer.
- **Werewolf** (`werewolf00`) — Bio: a person, underneath, caught by whatever's souring the ground at the
  worst possible angle — half the Wolf pack's ferocity, half the Ganger's desperate humanity. Voice:
  fights in growls, talks (on the rare occasion it can) in apologies. Needs advice about: that it isn't
  the Wolves and isn't the Gangers — it needs someone to tell the difference before the fight starts, not
  after, and nobody on the road has had the time to try.

## Sandsea Reaches — the clearest early proof the blight has a shape

The dunes keep what they take; Reaches enemies are less "native to the sand" than "what the sand gave
back once the Longmother's footfalls turned it over."

- **Flying Monkey** (`flyingmonkey`) — Bio: a dune-raider that steals before it fights, more nuisance
  than threat on its own, dangerous in the numbers the heat drives together. Voice: chattering, gleeful,
  treats a raid like a game it's winning. Needs advice about: that what it's stealing (rations, scrap,
  the caravan's own gear) is exactly what a dying oasis has none left to spare — it's starving the thing
  it's raiding.
- **Vulture** (`vulture`) — Bio: waits out every fight at a safe height, moves in only once something's
  already down; the desert's own Opportunist, with none of the Ripper's charm. Voice: wordless, circling,
  all patience and no urgency. Needs advice about: that a caravan that doesn't lose anyone permanently
  (`docs/BIBLE.md` §29.1) has nothing left for it to wait for — its whole strategy depends on a kind of
  loss this road no longer deals in.
- **Scorpion** (`scorpion`, close-quarters tagged) — Bio: unlike most of the Reaches' cast, fights close
  and precise rather than in a swarm — the one family here that reads more Duelist than feral beast.
  Voice: measured, strikes once and waits to see if it landed. Needs advice about: that precision aimed
  at the caravan is precision wasted — the thing actually worth that kind of focus is whatever's turning
  the sand strange underneath both of them.
- **Sand Maw** (`sandmaw`) — Bio: unseen until it isn't; the dune's own ambush predator, the clearest
  "something buried, now surfacing" story the Reaches tell with a body instead of a crawl paragraph.
  Voice: silence, then everything at once. Needs advice about: that surfacing to attack is the one thing
  guaranteed to get it noticed and fought — staying buried was the only strategy that was ever actually
  working for it.
- **Mecha Armor** (`mechaarmor00`–`mechaarmor02`) — Bio: the Reaches' first proof the blight isn't just
  rot with a smell (the crawl's own line) — "plating that grew instead of being forged," worn like armor
  by something that may not need the protection. Voice: mechanical cadence, no apparent operator, speaks
  (if it speaks) in status reports no one asked for. Needs advice about: unclear whether there's anyone
  left inside it to advise — which is itself the Reaches' open question, not a gap in this bio.
- **Purple Prototype** (`purpleprototype00`–`purpleprototype04`) — Bio: half-built, half-born, "a season
  ago" by the crawl's own reckoning — whatever made it wasn't finished and didn't wait to be. Voice:
  glitchy, repeats fragments of what might be instructions. Needs advice about: that it's running on an
  incomplete design, and the gentlest thing anyone could do for it is finish the job one way or the
  other, not keep fighting a half-made thing forever.
- **Tech Gremlin** (`techgremlin00`–`techgremlin02`) — Bio: small, fast, and drawn to anything that
  sparks — scavenges machine parts off whatever the caravan or the Reaches' other wrecks leave behind.
  Voice: chattery, acquisitive, narrates its own looting. Needs advice about: that the parts it's
  hoarding would do more good at the caravan's own forge-cart than wherever it's stashing them — it's
  solving the same materials shortage the party is, just alone and badly.

## Mireholt Fens — where the blight pools instead of drying off

"Every fen is worse than the last fen," the crawl says, and these families are proof of it: wetter
ground, older rot, and the first enemies that read as deliberately defending the fen rather than merely
surviving in it.

- **Hag** (`hag00`–`hag03`, two tiers reused as the Hex Sniper/Hex Warden hero portraits) — Bio: the fen's
  own ranged controller, lobbing curses from well outside melee range; some hags took up coin and left
  with the caravan (`heroes.md`'s Hex Sniper/Hex Warden), most didn't. Voice: barbed, economical, curses
  phrased like transactions. Needs advice about: that the two who left proved the fen isn't actually a
  life sentence — the ones who stayed are choosing the mire over the option they've already seen work.
- **Swamp Mistress** (`swampmistress00`, Ranged-tagged) — Bio: the Hag's more composed, more dangerous
  elder — a rot-home defender who reads as the fen's actual authority, not just one more creature in it.
  Voice: regal in a way that doesn't fit the mud, speaks like she remembers when this ground was worth
  ruling. Needs advice about: that defending the mire as if it were still worth the title is grief dressed
  as authority — there's nothing left here to be mistress of.
- **Lurker** (`lurker00`–`lurker02`) — Bio: waits half-submerged for the caravan's crews to pass over the
  wrong tile; the fen's quiet, personal ambush, where the Sand Maw's was loud and total. Voice: barely
  above a whisper, more threat implied than spoken. Needs advice about: that it's spent so long lurking
  it may not remember what it was waiting to do once something actually came close enough.
- **Marsh Shambler** (`marshshambler00`–`marshshambler01` & `03`) — Bio: mud and rot given rough shape and
  purpose, the fen's own slow, grinding bulk — built up from whatever sank here, not born. Voice: wet,
  heavy, every word (if it has them) sounds like it's being pulled up out of the muck. Needs advice
  about: that it's wearing the fen's dead like a body, and whatever's actually driving it probably isn't
  the fen itself — it's a puppet asking for advice it can't act on alone.
- **Tree Golem** (`treegolem00`–`treegolem04` & `06`, PlantBased+Mechanical tagged) — Bio: the single
  cleanest image of the biomechanical plague's whole premise: plant tissue and machine parts fused into
  one standing thing, not metaphorically, by content tag. Voice: creaking, slow, deliberate — speaks (if
  at all) the way old wood settles. Needs advice about: that it is, quite literally, the blight's answer
  to "what happens when something stands still too long" — and the only advice that would actually help
  it is the Longmother's own rule: keep moving, or this is what's next.
- **Naga** (`naga00`, Aquatic+Boss) — Bio: Mireholt Fens' boss-tier threat, a serpent-queen of the deep
  channels, older and more deliberate than anything else on this stretch of road. Voice: commanding, cold,
  speaks like it's addressing something far beneath it (which, from its vantage, the party is). Needs
  advice about: that the mire it rules is sinking the same way everywhere else is — sovereignty over
  ground that's actively dissolving isn't the win it thinks it is.

## Frostmaw Caverns — the first thing that was unmistakably *built*

Cold slows the blight here, which means the caves hold the oldest, least-disturbed threats on the whole
route — and the first proof (per the crawl) that something made the blight on purpose, not by accident.

- **Ceramic Knight** (`ceramicknight00`–`ceramicknight06`) — Bio: armor shaped like a Knight's, fired
  like pottery, animated by something that isn't a person inside it — Frostmaw's own stalwart-defender
  archetype gone wrong on purpose, not merely corrupted. Voice: hollow, formal, recites something that
  might once have been an oath. Needs advice about: that an oath recited by something that was never the
  one who swore it isn't a vow anymore, it's a recording — whatever's playing it back needs to be told
  the original speaker is long gone.
- **Cyclops** (`cyclops00`–`cyclops06`, lower tiers Boss-tagged) — Bio: Frostmaw's heavyweight, old
  enough to have been down here since before the mineral veins were first tapped, exactly the kind of
  thing cold-preserved time was always going to produce. Voice: slow, booming, genuinely confused more
  often than angry. Needs advice about: that the caravan passing through isn't the first thing to
  disturb this cave and won't be the last — guarding a single intruder when the whole route keeps coming
  is a fight that never actually ends in rest.
- **Ice Mauler** (`icemauler`, Magic+IceAffinity) — Bio: the caves' own caster-brute hybrid, channeling
  cold the way the Mage classes channel fire or frost spells, except the magic here still reliably works
  — the crawl's "cold keeps things slow, the blight included" cuts both ways. Voice: crackling, precise,
  the most articulate voice in the cavern's bestiary. Needs advice about: that the one place left where
  magic still works cleanly is exactly the place that should be sharing that fact with the caravan's own
  fraying Alchemist and Enchantress, not hoarding it as a weapon.
- **Mountain Troll** (`mountaintroll`) — Bio: Frostmaw's feral anchor, deep-place-born in the oldest,
  truest sense of the classes.json lore line that so many other families only borrow. Voice: guttural,
  territorial, more bothered than hostile. Needs advice about: that it was here first, genuinely, and the
  caravan isn't trying to take the cave from it — just pass through, the way it passes through everywhere,
  because stopping is the one thing it can't do either.
- **Oni** (`oni00`–`oni02`) — Bio: a demon-touched brute that reads more folklore than blight — horned,
  furious, closer to a classic ogre than to anything grown-and-built elsewhere in the caves. Voice: loud,
  blunt, speaks mostly in challenges. Needs advice about: that its fury is older than the plague and
  isn't the plague's fault — it's carrying a grudge that predates everything currently happening to this
  cave, and nobody's ever asked it against whom.
- **Yeti** (`yeti`, Beast+Humanoid) — Bio: the caves' coldest resident in temperament as well as climate,
  straddling beast and person the way Werewolf does in Green Valley, but settled into it rather than
  caught mid-change. Voice: sparse, patient, communicates more in posture than words. Needs advice about:
  that isolation this deep has let it stop needing anyone — which isn't peace, it's just a long enough
  silence that it forgot what company sounds like.

## Veshker Ruins — a dead city that never let its failure finish

Stone instead of carapace, built by people who tried to stand still forever and lost. Everything here is
some version of "what a city becomes once the thing that killed it never finished the job."

- **Cyber Zombie** (`cyberzombie00`–`cyberzombie04`, Mechanical+Undead) — Bio: Veshker's own fusion
  enemy, the city's dead wired into whatever power still runs under the ruins — "life ended, purpose
  remained" (the shared Undead lore line) except the purpose here is literally electrical. Voice: a dead
  voice looping through working machinery — speech as a recording, not a thought. Needs advice about:
  the Undead's own answer, worse: it isn't that it doesn't recognize rest is available, it's that
  something is actively keeping it from taking it, which makes this family's advice-giver a long-term
  project, not a conversation.
- **Demon Lord** (`demonlord`, Veshker-native, recurs as a title further down the road) — Bio: Veshker's
  own boss-tier horror, and per the Sandsea Reaches, Further Out crawl, not a single throne so much as "a
  shape the blight keeps finding again... wherever the host is strong enough to hold it" — this is simply
  the first one the caravan names with the title. Voice: grandiose, performs command the way the Captain
  performs order, except it's not hollow underneath — it believes the title. Needs advice about: that it
  is a condition, not a person, by the caravan's own blunt shorthand — the only advice that could possibly
  land is aimed at whoever gets strong enough to be next, not at this one, who's already past hearing it.
- **Ghost** (`ghost`) — Bio: a Veshker citizen who died believing the walls would hold; the city's own
  gentlest tragedy, more mournful than hostile. Voice: faint, trailing, repeats the same unfinished
  thought the Undead do, but quieter, more aware of how little weight it carries now. Needs advice about:
  that the walls it's still haunting already failed, visibly, around it — the advice it needs is simply
  being told, kindly, that it's allowed to stop waiting for them to hold.
- **Mannequin** (`mannequin`, Humanoid, played as unnatural/undead) — Bio: something wearing a person's
  shape without ever having been one — a display-piece animated by whatever's gone wrong in the ruins,
  closer to the Ceramic Knight's hollow recitation than to a real ghost. Voice: smooth, affectless,
  mimics speech without quite landing the rhythm of it. Needs advice about: that it was never given a
  self to begin with, which makes "advice" the wrong tool — what it actually needs is someone to notice
  it was built, not born, before deciding what (if anything) it owes the city it's defending.
- **Phantom** (`phantom`) — Bio: Veshker's more dangerous dead — less a mourning citizen than an idea of
  the city's old authority, refusing to let the rule it once enforced lapse. Voice: imperious, barely
  bothers to acknowledge the living are real enough to address. Needs advice about: that the authority
  it's still enforcing has no citizens left to govern — rule with nobody left to rule is just a habit
  wearing the shape of a job.
- **Reaper** (`reaper`) — Bio: not death itself, whatever the silhouette suggests — more a city official
  whose one remaining task (clearing the dead, maybe, or marking them) outlived every other function
  Veshker had. Voice: businesslike, oddly gentle, treats killing as paperwork rather than cruelty. Needs
  advice about: that the task it's performing stopped being necessary the moment the city actually died —
  there's no backlog left to clear, only the caravan passing through, which was never on its list.
- **Templar** (`templar00`–`templar05`) — Bio: Veshker's own stalwart defenders, the city's failed wall
  given a face — soldiers who swore to hold ground the way the crawl says the whole city tried and failed
  to. Voice: solemn, duty-bound, the most self-aware of Veshker's enemies about what that duty cost.
  Needs advice about: exactly what the crawl names as the city's whole mistake — that "walls built well
  enough" were never actually the same as learning to walk, and holding this particular ground, even
  now, repeats it.

### The Vampire Lord — see `heroes.md`, class `vampire`

Veshker Ruins' climax is the one enemy entry this file doesn't duplicate: the Vampire Lord is the
`vampire` class (`docs/BIBLE.md` §23 — "the spawn side decides the team"), covered fully under Striker →
Attrition in `heroes.md`. As an enemy he is the city's former ruler who found a way to keep "stopped"
going past when it should have killed him, and who wants the Longmother to stop too. The only named
enemy in the game who is also a playable hero is deliberately not re-written here — one bio, two fates.

## Remains & hazards — no voice, no advice, by design

`Gravestone`, `Bone Pile`, `Corpse` and `Naga Egg` (`docs/BIBLE.md` §14.5, §14.8) are board obstacles, not
combatants — the content itself gives them no `Abilities`, no `Team`, and a lore line that's really just a
play instruction ("break it before the dead climb back out"). They carry no voice and need no advice; they
exist so the families above have something to rise out of or hatch from. Worth naming here only so a
future writer doesn't go looking for a personality that was never meant to exist.
