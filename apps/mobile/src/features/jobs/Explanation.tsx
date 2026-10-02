import { useRouter } from 'expo-router';
import { View } from 'react-native';

import { useSimilarJobs } from '@/api/queries';
import type { MatchDetail } from '@/api/schemas';
import { dimensionLevelLabel } from '@/lib/format';
import { Avatar } from '@/ui/Avatar';
import { Badge, type BadgeTone } from '@/ui/Badge';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { Icon } from '@/ui/Icon';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

import { MatchBadge, categoryStyle } from './MatchBadge';

type Dimension = MatchDetail['dimensions'][number];

const levelTone: Record<Dimension['level'], BadgeTone> = { strong: 'success', medium: 'info', weak: 'warning' };
const levelSteps: Record<Dimension['level'], number> = { strong: 3, medium: 2, weak: 1 };

/** "How it fits you", axis by axis: a level (3-step meter + word) and one sentence. Deliberately no percentages. */
export function FitBreakdown({ dimensions }: { dimensions: Dimension[] }) {
  const { colors, radius } = useTheme();
  if (dimensions.length === 0) return null;

  return (
    <View style={{ gap: 14 }} testID="fit-breakdown">
      <Text variant="label" tone="subtle">Cómo encaja contigo</Text>
      {dimensions.map((d) => {
        const steps = levelSteps[d.level];
        const color = d.level === 'strong' ? colors.success : d.level === 'medium' ? colors.info : colors.warning;
        return (
          <View key={d.key} style={{ gap: 6 }} testID={`dimension-${d.key}`}>
            <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
              <Text variant="bodyStrong" style={{ flex: 1 }}>{d.label}</Text>
              <View
                style={{ flexDirection: 'row', gap: 3 }}
                accessibilityLabel={`${d.label}: ${dimensionLevelLabel[d.level]}`}
              >
                {[1, 2, 3].map((i) => (
                  <View key={i} style={{ width: 16, height: 6, borderRadius: radius.pill, backgroundColor: i <= steps ? color : colors.border }} />
                ))}
              </View>
              <Badge label={dimensionLevelLabel[d.level]} tone={levelTone[d.level]} size="sm" />
            </View>
            <Text variant="caption" tone="muted">{d.note}</Text>
          </View>
        );
      })}
    </View>
  );
}

/** What-if suggestions from the matching engine. The wording always makes clear it is only for skills the user really has. */
export function ImprovementsCard({ improvements, onEditProfile }: { improvements: MatchDetail['improvements']; onEditProfile: () => void }) {
  const { colors } = useTheme();
  if (improvements.length === 0) return null;

  return (
    <Card style={{ gap: 14 }} testID="improvements-card">
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
        <View style={{ width: 36, height: 36, borderRadius: 18, backgroundColor: colors.accentTint, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="trending-up" size={20} tone="accent" />
        </View>
        <View style={{ flex: 1 }}>
          <Text variant="heading">Cómo mejorar esta compatibilidad</Text>
          <Text variant="caption" tone="muted">Solo si ya lo sabes hacer: no agregues lo que no tienes.</Text>
        </View>
      </View>

      {improvements.map((item) => (
        <View key={item.title} style={{ gap: 6, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.border }}>
          <Text variant="bodyStrong">{item.title}</Text>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
            <Text variant="caption" tone="muted">Pasaría a</Text>
            <MatchBadge category={item.resultCategory} size="sm" />
          </View>
        </View>
      ))}

      <Button label="Editar mis habilidades" icon="create-outline" variant="secondary" size="sm" onPress={onEditProfile} testID="improvements-edit" />
    </Card>
  );
}

/** Offers that mean the same thing (nearest vectors). Hidden when there is nothing useful to show. */
export function SimilarJobs({ jobId }: { jobId: string }) {
  const router = useRouter();
  const similar = useSimilarJobs(jobId);

  if (similar.isLoading) {
    return (
      <View style={{ gap: 10 }}>
        <Text variant="heading">Ofertas parecidas</Text>
        <Skeleton height={64} radius={16} />
        <Skeleton height={64} radius={16} />
      </View>
    );
  }
  const items = similar.data ?? [];
  if (similar.isError || items.length === 0) return null;

  return (
    <View style={{ gap: 10 }} testID="similar-jobs">
      <Text variant="heading">Ofertas parecidas</Text>
      {items.map(({ job, match }) => (
        <Card key={job.id} onPress={() => router.push({ pathname: '/job/[id]', params: { id: job.id } })} style={{ padding: 12 }} testID={`similar-${job.id}`}>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
            <Avatar name={job.company} size={44} />
            <View style={{ flex: 1, gap: 3 }}>
              <Text variant="bodyStrong" numberOfLines={1}>{job.title}</Text>
              <Text variant="caption" tone="muted" numberOfLines={1}>{job.company}{job.district ? ` · ${job.district}` : ''}</Text>
              {match ? <MatchBadge category={match.category} size="sm" /> : null}
            </View>
            <Icon name="chevron-forward" size={18} tone="subtle" />
          </View>
        </Card>
      ))}
    </View>
  );
}

export { categoryStyle };
