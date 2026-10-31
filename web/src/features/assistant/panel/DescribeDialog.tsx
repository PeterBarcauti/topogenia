// DescribeDialog.tsx
// « Décrire un processus » from the home page (administrator): the panel of the assistant on a project yet to create, its proposal opened as a new project after its code and name are confirmed (spec 3.0 section 3.2).

import { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Dialog } from '../../../shared/ui/Dialog.tsx';
import { Icon } from '../../../shared/ui/Icon.tsx';
import { OpenPmapDialog } from '../../project/files/OpenPmapDialog.tsx';
import { readSettings } from '../assistantSettings.ts';
import { AssistantThread } from './AssistantThread.tsx';
import { createAssistantStore } from './assistantStore.ts';
import { Composer } from './Composer.tsx';

export function DescribeDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation();
  const [store] = useState(() => createAssistantStore({ mapId: () => null, selection: () => [] }));
  const [where, setWhere] = useState<string | null>(null);
  const [creating, setCreating] = useState<string | null>(null);
  // The settings say whether a model is called or the prompt copied, and where the data go (spec 3.0 section 6).
  useEffect(() => {
    const controller = new AbortController();
    readSettings(controller.signal).then((settings) => {
      const model = settings.connector === 'anthropic' || settings.connector === 'bedrock';
      store.getState().setMode(model ? 'model' : 'copy_paste');
      store.getState().setBounds(settings.files);
      setWhere(!model ? t('assistant.panel.destination_copy_paste')
        : t('assistant.panel.destination', {
          where: settings.connector === 'bedrock' ? t('assistant.panel.destination_bedrock', { region: settings.region ?? '' }) : t('assistant.panel.destination_anthropic'),
          model: settings.model ?? '', classification: t('classification.internal'),
        }));
    }).catch(() => {
      // Without the settings the exchange is tried: the host says why it cannot.
    });
    return () => {
      controller.abort();
    };
  }, [store, t]);
  if (creating !== null) return <OpenPmapDialog content={creating} onClose={onClose} />;
  return (
    <Dialog title={t('assistant.panel.describe_title')} testId="describe-dialog" onClose={onClose} wide>
      <div className="describe-panel" data-testid="describe-panel">
        {where !== null && <p className="assistant-destination describe-where" data-testid="assistant-destination"><Icon icon={ShieldCheck} size={14} />{where}</p>}
        <AssistantThread context={{ store, status: null, home: true, createProject: (pmap) => { setCreating(pmap); } }} />
        <div className="assistant-foot">
          <Composer store={store} placeholder={t('assistant.panel.placeholder')} />
          <p className="assistant-note">{t('assistant.panel.note')}</p>
        </div>
      </div>
    </Dialog>
  );
}
