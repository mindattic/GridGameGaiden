# Camp NPCs — the five who join the caravan along the way

Unlike heroes and enemies, these five already have shipped, in-voice dialogue (`dialogues.json`'s
`*_road_*`/`*_after_*`/`*_smith`/`*_alchemist`/`*_enchantress` join scenes and the five `camp_*_chat`
beats, `docs/BIBLE.md` §25.0.1/§27.1). Every bio below is written to agree with lines already in the game,
not invent a new voice for them — quoted lines are pulled straight from `dialogues.json` so a future
writer can see exactly which words are load-bearing canon and which are this file's own extrapolation.

## Wandering Merchant

**Joins:** Green Valley I, `greenvalley_road_merchant` (always; every branch ends with him joining).
**Service:** the Merchant (`docs/BIBLE.md` §25.1).

**Bio:** Rides a few wagons back of whatever crew is clearing the road ahead, same as he's done since
the wells turned — "I've been riding a few wagons back of every crew since the wells turned, and a
caravan this size could use another set of wheels." Heard about the party before meeting them: "you're
the ones who dropped at the last footfall, aren't you? I heard you cleared it before the ground even
finished shaking." Sells stock, runs the Bargain shelf of worn gear, and — per the Camp Talk's own open
choice — can be asked to let his caravan guard join as a Sellsword for 300 G (`heroes.md`'s Sellsword
entry is written to answer to exactly this hire).

**Voice:** Pragmatic, dry, never oversells. Treats commerce as a plain fact of survival, not a hustle —
"here, the roads past the valley, marked true, and a tonic for the nerves. On the house, since you're
paying for the ink." Rarely sentimental, but the one line that slips past the patter — "don't let that
caravan guard talk you into anything expensive" — says plenty about who he actually trusts.

**Needs advice about:** Whether riding a few wagons back still counts as traveling alone. He frames every
transaction as business, never admits the caravan is the first company he's kept this long since the
wells turned — the advice he needs is permission to say that out loud instead of pricing it.

---

## Innkeeper

**Joins:** after Green Valley I, `greenvalley_after_first` (StageCleared trigger). **Service:** the Inn
(`docs/BIBLE.md` §25.3.1), including the Medic Wagon.

**Bio:** Offers "a wagon with real beds, not bedrolls on a cart floor — first good sleep since the wells
turned sour" to a crew that's just finished its first real fight. Charges "a fair price for people who
clear footfalls for a living," which in practice means barely anything. Her Camp Talk doubles as the
caravan's informal news desk: "travelers talk more than they think when they're warm and fed. I just
listen — you'd be surprised what a crew says the night after a bad footfall."

**Voice:** Warm, practical, carries the night shift's particular calm. Doesn't comfort with words so
much as with logistics — hot water, a made bed, a fair price — and lets the comfort speak for itself.
Her best line reframes the whole road in one breath: "the Longmother doesn't stop, but you're allowed
to."

**Needs advice about:** That listening to everyone else's worst nights, for pay, is its own kind of
burden she's never let anyone carry back for her. She knows more about the caravan's state of mind than
anyone else in it and treats that knowledge as a service rendered, never a weight she's allowed to set
down.

---

## Blacksmith

**Joins:** after Green Valley II, `greenvalley_smith`. **Service:** the Blacksmith — Forge, Upgrade,
Salvage, Repair (`docs/BIBLE.md` §25.2).

**Bio:** Meets the party already reading the damage on their gear before anyone's said a word: "those
edges are chewed to the hilt. Whatever you've been fighting, it's eating your steel faster than you can
swing it." Learned the trade from his mother, "same as I do now" — "my mother's forge, a long time
before the wells turned. She worked shed-plate same as I do now. Guess some trades just keep walking."
That line is this NPC's whole thesis: forging is a trade that migrates with whoever's walking, has for at
least two generations.

**Voice:** Blunt, competent, unsentimental about his craft right up until he isn't — the mother line
lands precisely because everything around it is shop talk ("mind the sparks," "bring me something
broken"). Treats a request to prove the gear works first (one of the join scene's choices) without
offense; he'd rather be judged by results.

**Needs advice about:** Whether a trade that "just keeps walking" is inheritance or a trap — he's proud
of the lineage and has never once asked whether he'd have chosen it himself, on ground that wasn't
already moving.

---

## Alchemist

**Joins:** once the party has looted enough for a first brew (at the latest at the start of Sandsea Reaches), `desert_alchemist`. **Service:** the Alchemist — Brew
(`docs/BIBLE.md` §25.3). *(Note: this is the Camp NPC, distinct from the playable `alchemist` hero class
in `heroes.md`'s Healer → Aegis Wardens cluster — two characters, same craft, same name, never
conflated in dialogue.)*

**Bio:** Found working alone in the dunes, surrounded by flasks she's candid about not fully trusting
herself: "careful with those flasks — half of them are older than you, and the other half I'm honestly
not sure what's in anymore." Brews from whatever the crews bring back off the things they kill — "venom,
ichor, scale — it's all reagent if you know how to read it" — which is the in-fiction reason the drop
tables (`docs/BIBLE.md` §24.7) feed Brew the way they do.

**Voice:** Blunt about risk in a way that reads as honesty, not recklessness — she'll tell you flatly
when she doesn't know what something does ("don't touch the green one. What do you need?"). Her stated
reason for joining is the most quietly ominous line any Camp NPC gets: "the desert's not safe for a
brewer working alone, and lately neither is a clean spell" — she's naming the magic-fraying premise out
loud before the bible catches up to her.

**Needs advice about:** That "not sure what's in it anymore" describes more than one flask in her wagon
— she's noticed the world's magic going strange faster than anyone else in the caravan, said so once,
and then went back to treating it as a brewing-supply problem instead of the omen it actually is.

---

## Enchantress

**Joins:** at the start of Mireholt Fens, `swamp_enchantress`. **Service:** the Enchantress — Enchant
(`docs/BIBLE.md` §25.3.4).

**Bio:** Found by her own lights in the fen, the first person on the road who knew to "bind a working
tight enough to survive the interference the wetter ground throws at it, rather than cast one loose and
hope" — the Fens crawl's own framing of her arrival. Runes sewn into steel instead of spoken aloud: "one
who binds workings into steel instead of trusting them to the air — which, out here, the air doesn't
deserve." Her Camp Talk keeps the same register after she's settled in: "the runes are restless tonight...
they hum, faint, when something's close that magic doesn't like the shape of."

**Voice:** Watchful, a little eerie, speaks about magic the way a sailor speaks about weather — something
read, not commanded. Economical with reassurance; "sleep lightly. I'll be listening to the steel" is
comfort and warning in the same breath.

**Needs advice about:** Whether binding magic into objects, because the air itself can no longer be
trusted, is adaptation or surrender. She's already solved the caravan's immediate problem (spells that
hold where spoken ones don't) and has never let herself ask whether that solution is buying time or just
quietly admitting the old way of casting is already gone.

---

## How the five fit together

Read in join order — Merchant, Innkeeper, Blacksmith, Alchemist, Enchantress — they trace the exact arc
`docs/BIBLE.md` §1/§27.1 describes: a town that still trades and rests normally (Merchant, Innkeeper) in
the one place that hasn't fully turned yet, giving way to a town that has to repair what's breaking
faster than expected (Blacksmith), then to two people who've each independently noticed magic itself is
going strange and built their whole craft around coping with it before anyone official said so out loud
(Alchemist, Enchantress). None of them frame it as a crisis in their own dialogue — that restraint is
itself a voice note for anyone writing them further: these five understate, consistently, and let the
player notice the pattern instead of being told it.
