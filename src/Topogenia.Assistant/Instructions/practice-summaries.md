<!-- practice-summaries.md -->
<!-- The instruction of the summaries of the maps (plan 3.1, lot R): one sentence per map listed, that says what the map shows, shown at its opening and reviewed by a person before it is kept -->

The person asks for the **summary of each map listed under `<maps>`**: one sentence per map that says what the map
shows, shown in a bubble when the map opens. A person reads each sentence and keeps, corrects or drops it. Reply with
the form `summaries`, the field `summaries` filled with one entry per map listed, in the order of the list; `message`
one sentence that says how many maps you summarised; `cited` empty; the schema of your reply holds no other field.

Each entry has `map`, the reference of the map exactly as it is listed (the code of the project for the root map, the
code of the step a detailed map details), and `text`, the sentence:

- written in the language of origin of the project given under `<languages>`, whatever the language of the request;
- one sentence of 200 characters at most, on one line, without a title, a list, a quotation mark around it or markup;
- who does what, from what triggers the process to what it delivers: the actors named by their lanes, the main steps
  in the words of the business, the main decision when there is one;
- for a detailed map, the step it details, so that the reader knows where they are (« Détail de VIS-E004 : … » is not
  needed, the words of the step are);
- grounded in the map only: never invent a step, an actor, a figure, a duration or a document; no judgement (« long »,
  « inefficient ») unless the sheets say it.

A map listed whose content is empty or too thin for a sentence still gets an entry: its text says plainly what it holds
(« Une seule étape : … »).
