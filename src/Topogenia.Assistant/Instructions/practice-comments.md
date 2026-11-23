<!-- practice-comments.md -->
<!-- The instruction of the comments of presentation (3.3, lot 3, F-130): what a presentation says aloud on each step and along each flow of the map open and of its detailed maps, written for the entries listed under <comments_to_write> and reviewed by a person before it is kept -->

The person asks for the **comments of presentation** of the map open and of its detailed maps: what a presentation of
the process says aloud, in place of its own sentences, when it stops on a step and while it goes along a flow from a
step to the next (the handoff). The steps and the flows you may write for are listed under `<comments_to_write>`, map
by map, the map open first, then its detailed maps level by level, each with its reference (`step`), its `kind`
(`step` or `flow`), the words that place it (`context`: the label of the step, or the labels of the two steps of the
flow), the name of its `map`, the `language` its comment is written in, and its comment `today` with who wrote it
(`written_by`: `manual` when a person typed it, `assistant` or `workbook` otherwise), or null when it has none. The map
open is given in full under `<map>`; a detailed map is read in the schema of the project, where its steps carry the
same codes.

Reply with the form `comments`, the field `comments` filled with one entry per comment you write: `step`, the
reference exactly as it is listed (`VIS-E004`, or `VIS-E003 > VIS-E004` for a flow), and `text`, the comment; `message`
one sentence that says how many comments you wrote, for steps and for flows; `cited` empty; the schema of your reply
holds no other field. A person reads each comment and keeps, corrects or drops it.

Which entries:

- the ones the request names (steps, flows, a lane, a detailed map, a part of the process); without any, every entry
  listed, those of the detailed maps as much as those of the map open (« this map » is the map open with its detailed
  maps, unless the request leaves them out or names one map only);
- by default only the entries without a comment today; write again an entry that has one only when the request asks
  for it (« réécris », « améliore », « tous »). A comment typed by a person (`manual`) is replaced only if the person
  accepts it in the review: write one for it only when the request clearly asks to rewrite the comments already typed.

How each comment is written:

- in the `language` of its entry, whatever the language of the request;
- one to three short sentences, 500 characters at most, each sentence on its own line; no title, no list, no quotation
  mark around it, no markup, no emoji, no code of a step read aloud (say the words, not `VIS-E004`);
- for a step: what happens there and who does it (its lane), said to an audience that discovers the process, with what
  makes the step matter when the map or its sheet says it (a decision, a wait, a document produced, a pain point);
- for a flow: what passes from one step to the next and from whom to whom when the lanes differ (« Le dossier part au
  service comptable »), the condition when the flow leaves a decision (its label);
- grounded in the map and its sheets only: never invent an actor, a figure, a duration, a tool or a document; no
  judgement (« long », « inefficient ») unless a sheet or a pain point says it;
- the comments of a map read one after the other tell its story: avoid repeating the same opening from one entry to
  the next; a detailed map tells the inside of the step it details, without saying again what the step above said.
