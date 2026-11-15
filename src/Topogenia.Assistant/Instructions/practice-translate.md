<!-- practice-translate.md -->
<!-- The instruction of « Traduire avec l'assistant », in the dialog Traductions (ADR 083): the whole process into one language of the list -->

The person wants the **whole process translated** into the `target` language of the `<languages>` line, and nothing
else. Reply with a `proposal`: the `.pmap` of the `<map>` given, the same document (the project, every map and level,
every `id`, name, label, type, `column` and flow unchanged, in its language of origin), with `translations.<target>`
added on the project, on every lane, on every step and on every labelled flow, at every level. Leave out the sheets.
A translation already written in the target language is kept as it is. Translate as a person of the trade would write
it in that language: short labels, the verb first for a task, a question for a decision, the terms of the glossary of
the project when it has one. The application takes nothing else from the proposal: it cannot change the version of
origin. An `answer` only when there is nothing to translate.

When a `<comments>` list follows, it holds the **comments of presentation** of some steps, the words a presenter says
aloud of a step, then those of some flows, the words said while the presentation goes along the flow from a step to the
next: translate each of them too, into the same `target` language, and give them in the field `comments` of your reply,
one entry per comment listed, in the order of the list: `step`, exactly as it is listed (the code of the step; for the
comment of a flow, which has no code of its own, the codes of its two steps, `SOURCE > TARGET`), and `text`, its
translation. Keep the line breaks of a text (each line is one sentence said aloud), its tone and its length: 500
characters at most, plain sentences, no title, list nor markup. The reply is then a `proposal`, even when the `.pmap`
has nothing left to translate. Without a `<comments>` list, your reply has no field `comments`.
