import { View } from 'react-native';

import { useSources } from '@/api/queries';
import type { JobSourceInfo } from '@/api/schemas';
import { formatRelativeTime } from '@/lib/format';
import { BottomSheet } from '@/ui/BottomSheet';
import { ErrorState } from '@/ui/EmptyState';
import { Icon, type IconName } from '@/ui/Icon';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

const KIND: Record<JobSourceInfo['kind'], { label: string; icon: IconName }> = {
  demo: { label: 'Demostración', icon: 'flask-outline' },
  api: { label: 'API oficial', icon: 'git-network-outline' },
  feed: { label: 'Feed publicado por la empresa', icon: 'radio-outline' },
  careerPage: { label: 'Página de empleos pública', icon: 'globe-outline' },
  manual: { label: 'Cargada a mano', icon: 'create-outline' },
};

/** Transparency: which sources feed the agent, how fresh they are and whether they are working. */
export function SourcesSheet({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const { colors, radius } = useTheme();
  const sources = useSources();

  return (
    <BottomSheet visible={visible} onClose={onClose} title="¿De dónde salen las ofertas?">
      <Text tone="muted">
        Solo usamos fuentes que lo permiten: APIs oficiales, feeds que publican las propias empresas y páginas públicas cuyos términos lo autorizan.
        Cuando una oferta aparece en varios sitios, te la mostramos una sola vez.
      </Text>

      {sources.isLoading ? (
        <View style={{ gap: 10 }}><Skeleton height={56} radius={14} /><Skeleton height={56} radius={14} /></View>
      ) : sources.isError ? (
        <ErrorState message={sources.error.message} onRetry={() => void sources.refetch()} />
      ) : (
        <View style={{ gap: 8 }} testID="sources-list">
          {(sources.data ?? []).map((s) => (
            <View key={s.key} style={{ flexDirection: 'row', alignItems: 'center', gap: 12, padding: 12, borderRadius: radius.md, backgroundColor: colors.bg }}>
              <View style={{ width: 38, height: 38, borderRadius: 12, backgroundColor: colors.primaryTint, alignItems: 'center', justifyContent: 'center' }}>
                <Icon name={KIND[s.kind].icon} size={19} tone="primary" />
              </View>
              <View style={{ flex: 1, gap: 1 }}>
                <Text variant="bodyStrong" numberOfLines={1}>{s.name}</Text>
                <Text variant="caption" tone="muted" numberOfLines={2}>
                  {KIND[s.kind].label} · {s.activeOffers} {s.activeOffers === 1 ? 'oferta' : 'ofertas'}
                  {s.lastFetchedAt ? ` · ${formatRelativeTime(s.lastFetchedAt)}` : ''}
                </Text>
              </View>
              <View
                accessibilityLabel={s.healthy ? 'Funcionando' : 'Con problemas'}
                style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: s.healthy ? colors.success : colors.warning }}
              />
            </View>
          ))}
          {sources.data?.length === 0 ? <Text tone="muted">Todavía no hay fuentes conectadas.</Text> : null}
        </View>
      )}
    </BottomSheet>
  );
}
