# Space Arena Proposal

## Vision

`Space Arena` is a short-session 2D auto-battler where alien squads collide inside a shrinking orbital arena. The player acts as a coach, not a pilot: choose a starting squad, watch the automated fight, then draft one of three random mutations between waves.

## Core Loop

1. Pick a starting squad archetype: Plasma Swarm, Titan Lancers, or Comet Riders.
2. Watch an automatic arena wave resolve through AI movement, mount-style opening charges, weapon cooldowns, and projectile pressure.
3. Draft one of three mutations, or reroll when the offered choices do not fit the build.
4. Earn scrap, survive harder waves, and push the squad toward a powerful combo.

## Combat Pillars

- Auto-battle agency: player decisions happen before and between rounds.
- Controlled randomness: augments are random, but rerolls and build identity keep the run legible.
- Spatial pressure: the death zone shrinks every wave, punishing passive tanks and forcing collisions.
- Fast readability: each wave targets a 20-30 second decision cycle for WebGL-friendly sessions.

## Initial Archetypes

- Plasma Swarm: more projectiles and fire rate, but lower health.
- Titan Lancers: high health and armor, slower repositioning.
- Comet Riders: faster mount charge and stronger early tempo.

## Prototype Scope

The current Unity prototype includes:

- Runtime-generated arena scene.
- 3v3 automated combat.
- Alien sprites loaded from the existing repository assets.
- Shrinking death zone.
- Opening charge behavior.
- Projectile weapons.
- Three-card augment draft.
- Reroll economy.
- WebGL build menu item.

## Next Milestones

- Add persistent out-of-run progression and unlockable squads.
- Convert single-frame poses into full animation clips.
- Add mount visuals and collision-based opening impact.
- Add enemy faction rules and boss waves.
- Balance augment rarity, synergies, and anti-runaway mechanics.
