// presenterStore.test.ts
// The state of a presentation in the editor of the map: started from the whole map or a step, paused until the presenter plays it (lot J10); played, paused, moved, sped up or slowed down, blanked, stopped.

import { describe, expect, it } from 'vitest';
import type { Scene } from './model/script.ts';
import { createPresenterStore } from './presenterStore.ts';

const SCENES: Scene[] = [{ kind: 'opening' }, { kind: 'step', stop: 0 }, { kind: 'passage', stop: 0 }, { kind: 'step', stop: 1 }, { kind: 'closing' }];

describe('the state of a presentation', () => {
  it('is off until started; a start waits on the opening, paused, until the presenter plays it; a new run each time (lot J10)', () => {
    const store = createPresenterStore();
    expect(store.getState().active).toBe(false);
    store.getState().start('s2');
    const started = store.getState();
    expect([started.active, started.from, started.player, started.screen]).toEqual([true, 's2', { scene: 0, elapsed: 0, playing: false, speed: 1 }, 'map']);
    // Nothing runs until it is played.
    store.getState().tick(300, [1000, 1000]);
    expect(store.getState().player.elapsed).toBe(0);
    store.getState().toggle();
    expect(store.getState().player.playing).toBe(true);
    store.getState().start(null);
    expect([store.getState().from, store.getState().run, store.getState().player.playing]).toEqual([null, started.run + 1, false]);
  });

  it('runs its clock while it plays, pauses and plays again, and stops', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().toggle();
    store.getState().tick(300, [1000, 1000]);
    expect(store.getState().player.elapsed).toBe(300);
    store.getState().toggle();
    store.getState().tick(300, [1000, 1000]);
    expect(store.getState().player).toEqual({ scene: 0, elapsed: 300, playing: false, speed: 1 });
    store.getState().stop();
    expect(store.getState().active).toBe(false);
  });

  it('plays again from the opening once the end is reached', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().toggle();
    store.getState().tick(5000, [1000, 1000]);
    expect(store.getState().player.playing).toBe(false);
    store.getState().toggle([1000, 1000]);
    expect(store.getState().player).toEqual({ scene: 0, elapsed: 0, playing: true, speed: 1 });
  });

  it('moves by the moves of the player, a blank screen back to the map', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().blank('black');
    store.getState().move('next', SCENES);
    expect([store.getState().player.scene, store.getState().screen]).toEqual([1, 'map']);
    store.getState().blank('white');
    store.getState().blank('white');
    expect(store.getState().screen).toBe('map');
  });

});

describe('the moves of a presentation by its exits, its jumps and its speeds', () => {
  it('goes by an exit chosen at a decision: to the passage of the stop that takes it, its flight playing at once; nowhere for a stop without a passage (lot J10)', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().move('next', SCENES);
    store.getState().blank('black');
    store.getState().choose(0, SCENES);
    expect([store.getState().player, store.getState().screen]).toEqual([{ scene: 2, elapsed: 0, playing: true, speed: 1 }, 'map']);
    // The last stop has no passage: the story stays where it is.
    store.getState().choose(1, SCENES);
    expect(store.getState().player.scene).toBe(2);
  });

  it('starts again from a step clicked off the tour: at that step at once, paused or playing as it was, at its speed, a new tour by the usual exits (audit 3.2, BUG-04)', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().toggle();
    store.getState().move('next', SCENES);
    store.getState().toggle();
    store.getState().faster();
    const { run, player } = store.getState();
    store.getState().jump('s5');
    const state = store.getState();
    expect([state.from, state.player, state.run, state.screen]).toEqual(['s5', { ...player, scene: 1, elapsed: 0 }, run, 'map']);
  });

  it('goes faster and slower through the speeds offered, in fine steps around the normal one, never past the ends', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    const seen = [store.getState().player.speed];
    for (let index = 0; index < 3; index++) {
      store.getState().faster();
      seen.push(store.getState().player.speed);
    }
    for (let index = 0; index < 6; index++) {
      store.getState().slower();
      seen.push(store.getState().player.speed);
    }
    // A voice of the system is hard to follow at 0.5 and past 1.25 (essai of the project owner, 19/11/2026).
    expect(seen).toEqual([1, 1.1, 1.25, 1.25, 1.1, 1, 0.9, 0.75, 0.75, 0.75]);
  });
});

describe('the text typed in the field of the comment written (ADR 099 revised, essai J6)', () => {
  const typed = { text: 'Brouillon', over: { text: '', version: 0 }, refused: false, conflict: null };

  /** A store with a field open on `s1`, a text typed and on its way to the host. */
  const sending = () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().write('s1');
    store.getState().type(typed);
    store.getState().send(true);
    return store;
  };

  it('opens a field with nothing typed and nothing on its way, one more field each time, even on the same step', () => {
    const store = createPresenterStore();
    expect([store.getState().typed, store.getState().sending, store.getState().fields]).toEqual([null, false, 0]);
    store.getState().write('s1');
    expect([store.getState().typed, store.getState().sending, store.getState().fields]).toEqual([null, false, 1]);
    store.getState().type(typed);
    store.getState().write('s1');
    expect([store.getState().typed, store.getState().sending, store.getState().fields]).toEqual([null, false, 2]);
    store.getState().write(null);
    expect(store.getState().fields).toBe(2);
  });

  it('keeps the text typed and says when it is on its way, only while a field is open', () => {
    const store = createPresenterStore();
    store.getState().type(typed);
    store.getState().send(true);
    expect([store.getState().typed, store.getState().sending]).toEqual([null, false]);
    store.getState().write('s1');
    store.getState().type(typed);
    store.getState().send(true);
    expect([store.getState().typed, store.getState().sending]).toEqual([typed, true]);
    store.getState().send(false);
    store.getState().type({ ...typed, refused: true });
    expect([store.getState().typed?.refused, store.getState().sending]).toEqual([true, false]);
  });

  it('forgets the text and what was on its way when the field closes, or the presentation starts again or stops', () => {
    type Store = ReturnType<typeof createPresenterStore>;
    const leaves: ((store: Store) => void)[] = [
      (store) => { store.getState().write(null); }, (store) => { store.getState().start(null); }, (store) => { store.getState().stop(); },
    ];
    for (const leave of leaves) {
      const store = sending();
      leave(store);
      expect([store.getState().writing, store.getState().typed, store.getState().sending]).toEqual([null, null, false]);
    }
  });

  it('forgets the text of a field left for another step, and a dive or a way back does nothing to a field with its text', () => {
    const store = sending();
    store.getState().write('s2');
    expect([store.getState().writing, store.getState().typed, store.getState().sending]).toEqual(['s2', null, false]);
    store.getState().type(typed);
    store.getState().dive('s2', { id: 'm2' } as never);
    store.getState().surface();
    expect([store.getState().writing, store.getState().typed]).toEqual(['s2', typed]);
  });
});

describe('the comments of presentation of a presentation (ADR 099)', () => {
  const comment = (stepId: string, text: string, language = 'fr') => ({ step_id: stepId, text, language, source: 'manual', version: 1 });

  it('keeps the lines of the comments read by map and by step, those read again replacing them, and the lines of a step written or removed', () => {
    const store = createPresenterStore();
    store.getState().keepComments('m1', [comment('s1', 'Un.'), comment('s2', 'Deux.'), comment('s2', 'Two.', 'en')]);
    store.getState().keepComments('m2', [comment('s9', 'Détail.')]);
    expect(store.getState().comments.m1).toEqual({ s1: [comment('s1', 'Un.')], s2: [comment('s2', 'Deux.'), comment('s2', 'Two.', 'en')] });
    store.getState().keepComments('m1', [comment('s2', 'Deux bis.')]);
    expect(store.getState().comments).toEqual({ m1: { s2: [comment('s2', 'Deux bis.')] }, m2: { s9: [comment('s9', 'Détail.')] } });
    store.getState().keepComment('m1', 's3', [comment('s3', 'Trois.'), comment('s3', 'Three.', 'en')]);
    store.getState().keepComment('m1', 's2', []);
    expect(store.getState().comments.m1).toEqual({ s3: [comment('s3', 'Trois.'), comment('s3', 'Three.', 'en')] });
  });

  it('pauses the story while a comment is written, and a start or a stop closes the writing', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().move({ stop: 0 }, SCENES);
    store.getState().write('s1');
    expect([store.getState().writing, store.getState().player.playing]).toEqual(['s1', false]);
    store.getState().write(null);
    expect(store.getState().writing).toBeNull();
    for (const leave of [() => { store.getState().start(null); }, () => { store.getState().stop(); }]) {
      store.getState().write('s1');
      leave();
      expect(store.getState().writing).toBeNull();
    }
  });

  it('does nothing on a move, a jump, play, an exit chosen, a blank screen, a dive or a way back while a comment is written: its text is never lost unseen (R2-05)', () => {
    const store = createPresenterStore();
    store.getState().start(null);
    store.getState().dive('s0', { id: 'm2' } as never);
    store.getState().move({ stop: 0 }, SCENES);
    store.getState().write('s1');
    const before = store.getState();
    const gestures = [
      () => { store.getState().move('next', SCENES); }, () => { store.getState().jump('s5'); }, () => { store.getState().toggle(); },
      () => { store.getState().choose(0, SCENES); }, () => { store.getState().blank('black'); }, () => { store.getState().dive('s1', { id: 'm3' } as never); },
      () => { store.getState().surface(); },
    ];
    for (const gesture of gestures) {
      gesture();
      const after = store.getState();
      expect([after.player, after.from, after.screen, after.map, after.levels, after.run, after.writing])
        .toEqual([before.player, before.from, 'map', before.map, before.levels, before.run, 's1']);
    }
  });
});
