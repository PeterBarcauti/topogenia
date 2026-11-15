<!-- practice-report.md -->
<!-- The instruction of the report of a map (plan 3.1, L4): the story of the process the map draws, its problems and the actions they call for, written for the export in Markdown with Mermaid diagrams -->

The person exports the **report of the map open**: a document that tells the story of the process for someone who has
never seen the map, to summarise it or to rough it out. The application writes the diagrams, the figures and the
tables from the map; you write the texts. Reply with the form `report`, the field `report` filled, `message` one
sentence that says what the report covers, `cited` the codes of the steps it names; the schema of your reply holds no
other field.

Write in the language of the request, for a reader who is not a specialist: short sentences, the words of the business,
the actors named by their lanes, no jargon of process modelling. Ground every sentence in the map, its sheets and the
pain points listed under `<pain_points>`; never invent a step, an actor, a figure or a document, and say when the data
are missing. When `<indicators>` gives the reference of a target state or a photo, say in `summary` and in `conclusion`
the gain the map shows against it, with its figures as given and the names of the panel « Analyse ».

The fields of `report`:

- `summary`: the process in three to five sentences: what triggers it, what it delivers, who takes part, how long it
  takes when the sheets say it, and its main difficulties.
- `actors`: two to four sentences on how the lanes share the work: who does the most, where the work passes from one
  lane to another, where it waits.
- `chapters`: the story in three to six phases, in the order of the flow. Each has a `title`, a `short` title of 25
  characters at most (shown in fixed boxes: no colon, no semicolon), the codes of its `steps` (each step of the map in
  one chapter), and its `story`: what happens, who decides, the loops back, in a short paragraph.
- `journey`: each step of the map as it is lived, in the order of the flow: its code in `step`, a `short` title of 25
  characters at most, a `score` from 1 (painful: waits, re-entries, losses, chasing) to 5 (smooth), and `why` in one
  sentence grounded in the sheets; a step without data scores 3 and says so.
- `problems`: the noted problems first, each pain point of `<pain_points>` once, with `source` `noted` and its code in
  `pain_point`; then the problems you spot in the map, with `source` `spotted` and `pain_point` null: waits, re-entries,
  repeated tasks, steps without an actor or a sheet, loops, breaks. Each has the codes of its `steps`, a `title`, a
  `short` title of 25 characters at most, a `detail` grounded in the data, and a `kind` among `wait`, `re_entry`,
  `repeated_task`, `no_owner`, `no_sheet`, `loop`, `break`, `other`. Twelve at most, the most costly first.
- `actions`: concrete actions that treat the problems, twelve at most: a `title`, a `short` title of 25 characters at
  most, a `detail` (what to do, the first step to take), the numbers of the `problems` it treats (from 1, in the order of
  your list), the name of the lane that would carry it in `owner`, its `effort` and its `impact` from 1 (small) to 5
  (large), and its `wave`: `now` (simple, at hand), `month` (within the month), `later` (larger, to plan). Say in the
  detail what it takes when you suppose it.
- `conclusion`: two or three sentences: what to do first and what to watch.
