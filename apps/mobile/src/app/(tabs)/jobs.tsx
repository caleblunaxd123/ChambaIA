import { useLocalSearchParams } from 'expo-router';
import { useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, RefreshControl, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import type { FeedFilters, FeedTab } from '@/api/endpoints';
import { useFeed, useOverview } from '@/api/queries';
import { type MatchCategory, matchCategory } from '@/api/schemas';
import { FilterSheet, countFilters } from '@/features/jobs/FilterSheet';
import { JobCard } from '@/features/jobs/JobCard';
import { categoryStyle } from '@/features/jobs/MatchBadge';
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

const TABS: FeedTab[] = ['forYou', 'new', 'saved'];
const isTab = (v: unknown): v is FeedTab => TABS.includes(v as FeedTab);
const isCategory = (v: unknown): v is MatchCategory => matchCategory.safeParse(v).success;

const TAB_SUBTITLE: Record<FeedTab, string> = {
  forYou: 'ordenadas por qué tan bien encajan contigo',
  new: 'que aún no revisas',
  saved: 'que guardaste para decidir con calma',
};

export default function JobsScreen() {
  const { colors, spacing } = useTheme();
  const insets = useSafeAreaInsets();
  // Home links here with ?tab=new or ?category=review: the params seed the screen state each time they change.
  const params = useLocalSearchParams<{ tab?: string; category?: string; at?: string }>();
  const paramKey = `${params.tab ?? ''}|${params.category ?? ''}|${params.at ?? ''}`;
  const [seededFrom, setSeededFrom] = useState<string | null>(null);
  const [tab, setTab] = useState<FeedTab>('forYou');
  const [search, setSearch] = useState('');
  const [filters, setFilters] = useState<FeedFilters>({});
  const [sheetOpen, setSheetOpen] = useState(false);

  if (seededFrom !== paramKey) {
    setSeededFrom(paramKey);
    if (isTab(params.tab)) setTab(params.tab);
    if (params.tab || params.category) setFilters(isCategory(params.category) ? { category: params.category } : {});
  }

  const q = useDebounced(search.trim());
  const effective = useMemo<FeedFilters>(() => ({ ...filters, q: q || undefined }), [filters, q]);
  const overview = useOverview();
  const feed = useFeed(tab, effective);
  const { open, save, unsave, discard, busy } = useJobActions();

  const items = feed.data?.pages.flatMap((p) => p.items) ?? [];
  const total = feed.data?.pages[0]?.total;
  const activeFilters = countFilters(filters);
  const hasAnyFilter = activeFilters > 0 || q.length > 0;
  const clearAll = () => {
    setFilters({});
    setSearch('');
  };

  const toggleModality = (m: NonNullable<FeedFilters['modality']>) => setFilters((f) => ({ ...f, modality: f.modality === m ? undefined : m }));

  const header = (
    <View style={{ gap: spacing.md, paddingBottom: spacing.md }}>
      <View style={{ gap: 2 }}>
        <Text variant="display">Empleos</Text>
        <Text tone="muted" testID="jobs-count">
          {total === undefined ? 'Buscando ofertas para ti…' : `${total} ${total === 1 ? 'oferta' : 'ofertas'} ${TAB_SUBTITLE[tab]}`}
        </Text>
      </View>

      <Input value={search} onChangeText={setSearch} placeholder="Buscar cargo o empresa" icon="search-outline" returnKeyType="search" autoCorrect={false} testID="jobs-search" />

      <SegmentedTabs
        value={tab}
        onChange={setTab}
        options={[
          { value: 'forYou', label: 'Para ti' },
          { value: 'new', label: 'Nuevas', count: overview.data?.newTotal },
          { value: 'saved', label: 'Guardadas' },
        ]}
      />

      <ScrollView horizontal showsHorizontalScrollIndicator={false} keyboardShouldPersistTaps="handled" contentContainerStyle={{ gap: 8, paddingRight: spacing.lg }} style={{ marginHorizontal: -spacing.lg, paddingHorizontal: spacing.lg, flexGrow: 0 }}>
        <Chip label={activeFilters > 0 ? `Filtros · ${activeFilters}` : 'Filtros'} icon="options-outline" selected={activeFilters > 0} onPress={() => setSheetOpen(true)} testID="open-filters" />
        {filters.category ? <Chip label={categoryStyle[filters.category].label} onRemove={() => setFilters((f) => ({ ...f, category: undefined }))} testID="chip-category" /> : null}
        {filters.district ? <Chip label={filters.district} icon="location-outline" onRemove={() => setFilters((f) => ({ ...f, district: undefined }))} /> : null}
        {filters.minSalary ? <Chip label={`Desde S/ ${filters.minSalary}`} onRemove={() => setFilters((f) => ({ ...f, minSalary: undefined }))} /> : null}
        {filters.postedWithinDays ? <Chip label={filters.postedWithinDays === 1 ? 'Hoy' : `Últimos ${filters.postedWithinDays} días`} onRemove={() => setFilters((f) => ({ ...f, postedWithinDays: undefined }))} /> : null}
        {(['remote', 'hybrid', 'onSite'] as const).map((m) => (
          <Chip key={m} label={modalityLabel[m]} selected={filters.modality === m} onPress={() => toggleModality(m)} testID={`quick-${m}`} />
        ))}
      </ScrollView>
    </View>
  );

  const empty = feed.isLoading ? (
    <JobListSkeleton />
  ) : feed.isError ? (
    <ErrorState message={feed.error.message} onRetry={() => void feed.refetch()} />
  ) : hasAnyFilter ? (
    <EmptyState icon="funnel-outline" title="Ninguna oferta coincide" message="Prueba quitando algún filtro o buscando con otras palabras." actionLabel="Quitar filtros" onAction={clearAll} />
  ) : tab === 'saved' ? (
    <EmptyState icon="bookmark-outline" title="Aún no guardas ofertas" message="Toca «Guardar» en una oferta y la verás aquí para decidir con calma." actionLabel="Ver ofertas para mí" onAction={() => setTab('forYou')} />
  ) : tab === 'new' ? (
    <EmptyState icon="checkmark-done-outline" title="Estás al día" message="Ya revisaste todo lo nuevo. Tu agente seguirá buscando y te avisará cuando aparezca algo para ti." actionLabel="Ver ofertas para mí" onAction={() => setTab('forYou')} />
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
          <JobCard
            item={item}
            busy={busy}
            onOpen={() => open(item.job.id)}
            onSave={() => save(item.job.id)}
            onUnsave={() => unsave(item.job.id)}
            onDismiss={() => discard(item.job.id, item.match?.status)}
          />
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
