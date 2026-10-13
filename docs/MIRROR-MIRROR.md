# Mirror Mirror

Status: designed and backlogged, not yet implemented. Specified by Matt
"Breakpoint" Heck on 2026-10-13. Points his specification leaves open are
marked *(proposal)* and listed under "Open Questions" and in
`docs/AI-requests-for-humans.txt`.

## The Gag

The mirror universe of the 1967 *Star Trek* episode "Mirror, Mirror":
an alternate dimension where everyone is their own opposite and wears a
goatee to prove it. The mod never says so. Its description is, as
specified:

> Allows pawns to pause and reflect on the possibility of alternate
> dimensions and/or facial hair.

Per the premise, it is cheap interdimensional travel, delivered decades
early, with the obvious risk left in.

Everything players see or hear is original: no names, images or audio
taken from the show. The device's own names (Mirror Mirror, Mirror Sword,
Agonizer) are the operator's.

## The Machine

- **A recreation building** *(proposal: 1 by 2, wall-adjacent)*. Pawns
  gaze into it for about a minute, as a recreation activity, for a small
  amount of recreation *(proposal: the vanilla "meditative" recreation
  kind, at about the rate of a plant pot)*.
- **Art, per the motif:** a full-length mirror in a riveted steel frame,
  with batshittium as the one focal glow. The glass shows a faint magenta
  shimmer where the other dimension leaks through *(proposal)*.
- **Research:** *(proposal)* a project Mirror Mirror, after the Brain
  Squeezer, the mod's other recreation device.

## The Activation

Each session has a 5% chance of going wrong. When it does:

- **The sting:** a 1960s orchestral sci-fi sting with ominous gravitas
  plays for a couple of seconds. It must be an original composition in
  that style (brass stab, string tremolo, timpani roll) and never the
  show's own cue. It is made, or commissioned, through the audio pipeline
  backlogged for the Colony Bomb.
- **The duplicate:** a duplicate of the gazing pawn steps out of the
  mirror: same name, face, body, age, skills and backstory, but with
  inverse morality and psychology and inverse facial hair.
- **Its weapon,** copied and then swapped:
  - a melee weapon becomes a **Mirror Sword**, with no special
    properties *(proposal: a longsword's stats with a mirrored look)*;
  - a ranged weapon becomes an **Agonizer**, a short-range weapon doing
    low damage and inflicting psychic distress.
- **Who it fights for:** an enemy faction, of course (specified
  2026-10-13). The duplicate arrives hostile, as its own raid of one, and
  can be downed, captured and recruited like any other enemy.
  *(Proposal:)* the enemy is the mod's own hidden, permanently hostile
  faction, "the other side of the glass". It is created on demand the
  first time a mirror misfires, so older saves need no new world, with
  vanilla's hidden hostile ancients as the fallback.

### Inverse Morality and Psychology (first draft)

Top-level only, as specified. Each trait of the original maps to its
opposite; a trait with no opposite is kept *(proposals)*:

| Original | Duplicate |
|----------|-----------|
| Kind | Psychopath |
| Psychopath | Kind |
| Bloodlust | Wimp (hates violence) |
| Wimp | Bloodlust |
| Abrasive | Kind |
| Greedy | Ascetic |
| Ascetic | Greedy |
| Masochist | Wimp |
| Transhumanist | Body purist |
| Body purist | Transhumanist |
| Pyromaniac | (removed) |
| Natural mood, nerves, industriousness | The same spectrum, negated (sanguine becomes depressive, iron-willed becomes volatile, industrious becomes lazy) |
| Neurotic or very neurotic | Removed |

Spectrum degrees negate exactly (+2 becomes −2). With Ideology active,
the duplicate gets the ideoligion most opposed to the original's
*(proposal; stretch goal)*.

### Inverse Facial Hair

This applies regardless of gender:

- **No beard (or stubble):** the duplicate gets a goatee, the canonical
  mirror-universe look.
- **Any beard:** the duplicate is clean-shaven.

Vanilla beards are style items that RimWorld assigns to men by default,
so the duplicate's beard is set directly on its style tracker. A smoke
check confirms it renders on women.

### The Agonizer

- **A short-range weapon** *(proposal: range about 6 tiles, a quick
  cooldown, about 4 damage per hit)*.
- **On a hit,** it adds an "agonized" hediff: pain that fades over a
  minute or so, and a short mood debuff that reads as psychic distress.
  It has no effect through psychic deafness, as vanilla psychic effects
  do *(proposals)*.
- It is a normal weapon once the duplicate is downed. Colonists can use
  it.

## Implementation Notes

- **Copying the pawn:** generate a new pawn of the same kind and faction
  template, then copy over the name, gender, age, body and head type,
  skin, hair, skills, backstories and health. That is safer than cloning
  vanilla's internal state. The name gets no suffix *(proposal)*, so the
  two are told apart by beard and behaviour.
- **The 5% roll:** made once per completed session, in the recreation
  job's finish action. Only one duplicate per session.
- **Harmony:** none needed. The machine is a building with its own job
  driver, and the duplicate is an ordinary generated pawn.
- **Tests:**
  - Core unit tests: the trait-inversion table, beard inversion, and
    the activation roll.
  - XML tests: the building, both weapons, the hediff and the sound
    defs.
  - Smoke checks: a session grants recreation; a forced activation
    spawns one hostile duplicate with the inverted traits, the inverted
    beard (tested on a woman too) and the swapped weapon; the Agonizer's
    hit applies its hediff; nothing logs an error.

## Open Questions

1. Which enemy faction: the mod's own hidden hostile faction (proposed),
   or an existing enemy such as the pirates?
2. The trait-inversion table: is the first draft above right in spirit?
3. Agonizer numbers: about 6 tiles, 4 damage, a minute of pain and a
   mood debuff?
4. Research and cost: after the Brain Squeezer?
5. Should the duplicate's name be marked (for example a suffix), or left
   identical?
