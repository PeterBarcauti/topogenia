// SearchHits.tsx
// The results of the panel « Rechercher » (ADR 110 point 5): by map, this map first, each map below under its name and its level; each result a button with the pictogram of its kind, the text round the words marked, its language when it is not the one shown, the step it belongs to; and what the panel says of the search.

import { Fragment, useMemo } from 'react';
import { FileText, Flame, ListChecks, Rows3, Wrench, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useStore } from 'zustand';
import type { Schema } from '../../../api/client.ts';
import { messageOf } from '../../../shared/i18n/errorMessage.ts';
import { nameIn } from '../../../shared/i18n/languageNames.ts';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { SHAPE_ICONS } from '../chrome/shapeIcons.ts';
import { excerptOf, marksOf, namedIn } from './searchResults.ts';
import { searchStore } from './searchStore.ts';
import { charactersOf } from './useSearch.ts';

type Hit = Schema<'SearchHitView'>;

/** The pictogram of each kind of result, those of the marks of a step for a document and a pain point. */
const KIND_ICONS: Readonly<Record<Schema<'SearchKind'>, LucideIcon>> = {
  step: SHAPE_ICONS.task, lane: Rows3, document: FileText, tool: Wrench, pain_point: Flame, action: ListChecks,
};

interface HitRowProps {
  readonly hit: Hit;
  readonly rank: number;
  readonly current: boolean;
  readonly shown: string;
  readonly onGo: (rank: number) => void;
}

function HitRow({ hit, rank, current, shown, onGo }: HitRowProps) {
  const { t, i18n } = useTranslation();
  const excerpt = excerptOf(hit.text, marksOf(hit));
  const kind = t(`search.kinds.${hit.kind}`);
  return (
    <li>
      <button type="button" className="search-hit" aria-current={current ? 'true' : undefined} data-testid={`search-hit-${String(rank)}`}
        onClick={() => { onGo(rank); }}>
        <span className="search-hit-head">
          <span className="search-hit-kind" role="img" aria-label={kind} title={kind}><Icon icon={KIND_ICONS[hit.kind]} size={14} /></span>
          <span className="search-hit-text">
            {excerpt.map((part, at) => (part.marked ? <mark key={at}>{part.text}</mark> : <Fragment key={at}>{part.text}</Fragment>))}
          </span>
          {hit.language !== null && hit.language !== shown && (
            <span className="search-hit-language" title={t('search.origin', { language: nameIn(hit.language, i18n.language) })}>{hit.language.toUpperCase()}</span>
          )}
        </span>
        {hit.step_code != null && (
          <span className="search-hit-step">
            <code>{hit.step_code}</code> {hit.step == null ? '' : namedIn(hit.step, shown)}
            {hit.status != null && <span> · {t(`sheet.pain_point.statuses.${hit.status}`)}</span>}
          </span>
        )}
      </button>
    </li>
  );
}

/** The results by map, in the order of the reply (the map asked first), each ranked in the list of all of them. */
export function SearchGroups({ mapId, shown, onGo }: { mapId: string; shown: string; onGo: (rank: number) => void }) {
  const { t } = useTranslation();
  const reply = useStore(searchStore, (state) => state.reply);
  const index = useStore(searchStore, (state) => state.index);
  // The rank of the first result of each map in the list of all of them.
  const firsts = useMemo(() => (reply?.maps ?? []).map((_, at, maps) => maps.slice(0, at).reduce((count, group) => count + group.hits.length, 0)), [reply]);
  return reply?.maps.map((group, at) => {
    const here = group.map_id === mapId;
    const name = here ? t('search.this_map') : namedIn(group.name, shown);
    return (
      <section key={group.map_id} className="search-group" aria-label={name}>
        <h3>{name}{!here && <span className="search-level"> {t('search.level', { level: group.depth })}</span>}</h3>
        <ul className="search-hits">
          {group.hits.map((hit, place) => {
            const rank = (firsts[at] ?? 0) + place;
            return <HitRow key={rank} hit={hit} rank={rank} current={rank === index} shown={shown} onGo={onGo} />;
          })}
        </ul>
      </section>
    );
  }) ?? null;
}

/** What the panel says of the search: under way, too short, a refusal, nothing found, the first results only. */
export function SearchMessage({ count }: { count: number }) {
  const { t, i18n } = useTranslation();
  const text = useStore(searchStore, (state) => state.text);
  const status = useStore(searchStore, (state) => state.status);
  const error = useStore(searchStore, (state) => state.error);
  const reply = useStore(searchStore, (state) => state.reply);
  if (status === 'error') return <p className="search-message is-error" role="alert">{messageOf(error)}</p>;
  if (status !== 'idle') return <p className="search-message">{t('search.searching')}</p>;
  if (reply === null) return charactersOf(text) === 1 ? <p className="search-message">{t('search.short')}</p> : null;
  if (reply.total === 0) return <p className="search-message">{t('search.none')}</p>;
  if (reply.total <= count) return null;
  const total = new Intl.NumberFormat(i18n.language).format(reply.total);
  return <p className="search-message">{t('search.too_many', { shown: count, total })}</p>;
}
