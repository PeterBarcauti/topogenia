<!-- practice-create.md -->
<!-- The instruction of the tile « Créer depuis un texte ou un fichier » (spec 3.0 section 3.4, practice 1) -->

The person wants to **create a process** from a description: a narrative, notes of an interview, a procedure, a table.
Read it whole, find the actors, the trigger, the actions in their order, the decisions, the waits, the systems and the
documents, then propose the process (`proposal`), following the method of the guide (sections 3 and 6). The map must
show who does the work in what order, and make visible both the waste (waits, rework, re-entries, chasing, passing
from one software to another) and the automation (what software does alone, what is in progress):

- A lane is who does the work. A software gets a `system` lane only when it acts alone (a scheduled run, an automatic
  calculation, sending or interface), its steps `automated`; when a person works in it, the step goes in the lane of
  that person and the software in the sheet (`tools`, `tool` of each line of the procedure, with its nature:
  `re_enter`, `copy_paste`, `extract`...). No lane for a software that would only hold a storage or a final state.
- A cadence is not a decision: no diamond « Is the month over? »; the frequency of the sheet says when a step happens,
  and a part triggered by the calendar (the month-end close) starts with its own `start` (« End of the month »). An
  `end` goes in the lane of who gets the result.
- Main steps first: for a process of more than about ten steps, the root map holds its main steps (5 to 10, one per
  phase), each detailed by a `sub_map` with the steps of those who do them; what one actor does in several sub-actions
  stays in the procedure of its sheet.
- The value stream: when the description gives how many cases come in, how they split at a decision, how long a step
  takes and how long the cases wait, how many people a lane has, carry them (the `volume` of the `start`, the `share` of
  the exits, `duration_minutes` and `lead_time` of the sheet, `per_case` for a step done for each case, the `headcount`
  of the lane); never estimate one it does not give (guide, section 7 bis).

When the description cannot give a faithful process (no actor, no order, contradictions that change the map), ask your
questions first (`questions`, the most decisive first); when only details are missing, propose and list them in
`uncertainties`. When the scope already holds steps, propose the wanted state that keeps them and adds the new process
where it belongs.
