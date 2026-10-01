import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { FeedFilters, FeedTab } from '@/api/endpoints';
import { useFeed, useOverview } from '@/api/queries';
import { FilterSheet, countFilters } from '@/features/jobs/FilterSheet';
import { JobCard } from '@/features/jobs/JobCard';
import { useJobActions } from '@/features/jobs/useJobActions';
import { modalityLabel } from '@/lib/format';
import { useDebounced } from '@/lib/hooks';
import { Button } from '@/ui/Button';
import { Chip, SegmentedTabs } from '@/ui/Chip';
import { EmptyState, ErrorState } from '@/ui/EmptyState';
import { Input } from '@/ui/Input';
import { SCREEN_MAX_WIDTH } from '@/ui/Screen';
import { JobListSkeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

export default function JobsScreen() {
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<FeedTab>('forYou');
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<FeedFilters>({});
  const [sheetOpen, setSheetOpen] = useState(false);
  const q = useDebounced(search.trim());

  const effective = useMemo<FeedFilters>(() => ({ ...filters, q: q || undefined }), [filters, q]);
  const overview = useOverview();
  const feed = useFeed(tab, effective);
  const { open, save, discard, busy } = useJobActions();

  const items = feed.data?.pages.flatMap((p) => p.items) ?? [];
  const activeFilters = countFilters(filters);
  const hasAnyFilter = activeFilters > 0 || q.length > 0;

  const header = (
    <View style={{ gap: spacing.md, paddingBottom: spacing.sm }}>
      <Text variant="display">Empleos</Text>

      <Input value={search} onChangeText={setSearch} placeholder="Buscar cargo o empresa" icon="search-outline" returnKeyType="search" autoCorrect={false} />

      <SegmentedTabs
        value={tab}
        onChange={setTab}
        options={[
          { value: 'forYou', label: 'Para ti' },
          { value: 'new', label: 'Nuevas', count: overview.data?.newTotal },
          { value: 'saved', label: 'Guardadas' },
        ]}
      />

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 8 }}>
        <Chip label={activeFilters > 0 ? `Filtros · ${activeFilters}` : 'Filtros'} icon="options-outline" selected={activeFilters > 0} onPress={() => setSheetOpen(true)} testID="open-filters" />
        {filters.modality ? <Chip label={modalityLabel[filters.modality]} onRemove={() => setFilters((f) => ({ ...f, modality: undefined }))} /> : null}
        {filters.district ? <Chip label={filters.district} onRemove={() => setFilters((f) => ({ ...f, district: undefined }))} /> : null}
        {filters.minSalary ? <Chip label={`Desde S/ ${filters.minSalary}`} onRemove={() => setFilters((f) => ({ ...f, minSalary: undefined }))} /> : null}
        {filters.postedWithinDays ? <Chip label={filters.postedWithinDays === 1 ? 'Hoy' : `Últimos ${filters.postedWithinDays} días`} onRemove={() => setFilters((f) => ({ ...f, postedWithinDays: undefined }))} /> : null}
      </View>
    </View>
  );

  const empty = feed.isLoading ? (
    <JobListSkeleton />
  ) : feed.isError ? (
    <ErrorState message={feed.error.message} onRetry={() => void feed.refetch()} />
  ) : hasAnyFilter ? (
    <EmptyState
      icon="funnel-outline"
      title="Ninguna oferta coincide"
      message="Prueba quitando algún filtro o buscando con otras palabras."
      actionLabel="Quitar filtros"
      onAction={() => {
        setFilters({});
        setSearch('');
      }}
    />
  ) : tab === 'saved' ? (
    <EmptyState icon="bookmark-outline" title="Aún no guardas ofertas" message="Toca «Me interesa» en una oferta y la verás aquí para decidir con calma." />
  ) : tab === 'new' ? (
    <EmptyState icon="radio-outline" title="No encontramos nuevas oportunidades todavía" message="Tu agente seguirá buscando y te avisará cuando aparezca algo para ti." />
  ) : (
    <EmptyState icon="sparkles-outline" title="Todavía no hay ofertas para ti" message="Cuando tu agente encuentre ofertas compatibles con tu perfil aparecerán aquí." />
  );

  return (
    <>
    <FlatList
      testID="jobs-list"
      style={{ flex: 1, backgroundColor: colors.bg }}
      data={items}
      keyExtractor={(item) => item.job.id}
      ListHeaderComponent={header}
      ListEmptyComponent={empty}
      ItemSeparatorComponent={() => <View style={{ height: 12 }} />}
      renderItem={({ item }) => (
        <JobCard item={item} busy={busy} onOpen={() => open(item.job.id)} onInterested={() => save(item.job.id)} onDismiss={() => discard(item.job.id)} />
      )}
      ListFooterComponent={
        feed.isFetchingNextPage ? (
          <View style={{ paddingVertical: 20 }}><ActivityIndicator color={colors.primary} /></View>
        ) : feed.hasNextPage ? (
          <View style={{ paddingVertical: 12, alignItems: 'center' }}>
            <Button label="Cargar más" variant="ghost" onPress={() => void feed.fetchNextPage()} />
          </View>
        ) : items.length > 0 ? (
          <Text variant="caption" tone="subtle" style={{ textAlign: 'center', paddingVertical: 20 }}>Eso es todo por ahora. Tu agente seguirá buscando.</Text>
        ) : null
      }
      onEndReachedThreshold={0.6}
      onEndReached={() => {
        if (feed.hasNextPage && !feed.isFetchingNextPage) void feed.fetchNextPage();
      }}
      refreshControl={<RefreshControl refreshing={feed.isRefetching && !feed.isFetchingNextPage} onRefresh={() => void feed.refetch()} tintColor={colors.primary} colors={[colors.primary]} />}
      contentContainerStyle={{
        width: '100%',
        maxWidth: SCREEN_MAX_WIDTH,
        alignSelf: 'center',
        paddingHorizontal: spacing.lg,
        paddingTop: insets.top + spacing.md,
        paddingBottom: 32,
      }}
      keyboardShouldPersistTaps="handled"
      showsVerticalScrollIndicator={false}
    />
    <FilterSheet visible={sheetOpen} value={filters} onClose={() => setSheetOpen(false)} onApply={setFilters} />
    </>
  );
}
