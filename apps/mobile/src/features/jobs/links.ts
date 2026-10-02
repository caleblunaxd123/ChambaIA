import type { FeedTab } from '@/api/endpoints';
import type { MatchCategory } from '@/api/schemas';

/**
 * Link to the Empleos tab with a tab/category preset. `at` makes every tap a new set of params, so tapping the same
 * shortcut twice re-applies it even if the user changed the tab in between (the screen re-seeds when params change).
 */
export function jobsLink(tab: FeedTab, category?: MatchCategory, at: number = Date.now()) {
  return { pathname: '/jobs' as const, params: { tab, ...(category ? { category } : {}), at: String(at) } };
}
