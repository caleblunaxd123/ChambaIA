import { type QueryClient, keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { useAuthStore } from '@/state/auth-store';

import { type FeedFilters, type FeedTab, type PreferencesInput, type ProfileInput, api } from './endpoints';
import type { ApplicationStatus } from './schemas';

/** One place for cache keys so invalidation can never drift from the queries. */
export const keys = {
  overview: ['overview'] as const,
  feed: (tab: FeedTab, filters: FeedFilters) => ['feed', tab, filters] as const,
  feedAll: ['feed'] as const,
  job: (id: string) => ['job', id] as const,
  applications: ['applications'] as const,
  profile: ['profile'] as const,
  preferences: ['preferences'] as const,
  resumes: ['resumes'] as const,
};


export function useOverview() {
  return useQuery({ queryKey: keys.overview, queryFn: api.matches.overview });
}

export function useFeed(tab: FeedTab, filters: FeedFilters) {
  return useInfiniteQuery({
    queryKey: keys.feed(tab, filters),
    queryFn: ({ pageParam }) => api.matches.feed(tab, filters, pageParam),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });
}

export function useJobDetail(id: string) {
  return useQuery({ queryKey: keys.job(id), queryFn: () => api.jobs.detail(id) });
}

/** Reference data (districts, skills) never changes during a session. */
export function useDistricts() {
  return useQuery({ queryKey: ["catalog", "districts"], queryFn: api.catalog.districts, staleTime: Infinity });
}

export function useSkillCatalog() {
  return useQuery({ queryKey: ["catalog", "skills"], queryFn: api.catalog.skills, staleTime: Infinity });
}

export function useApplications() {
  return useQuery({ queryKey: keys.applications, queryFn: api.applications.list });
}

export function useProfile(enabled = true) {
  return useQuery({ queryKey: keys.profile, queryFn: api.profile.get, enabled });
}

export function usePreferences() {
  return useQuery({ queryKey: keys.preferences, queryFn: api.preferences.get });
}

/** Everything derived from matches must be refetched after the user's data or preferences change. */
export function invalidateMatchData(client: QueryClient) {
  return Promise.all([
    client.invalidateQueries({ queryKey: keys.overview }),
    client.invalidateQueries({ queryKey: keys.feedAll }),
    client.invalidateQueries({ queryKey: ['job'] }),
    client.invalidateQueries({ queryKey: keys.applications }),
  ]);
}

export function useSeenMutation() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (jobId: string) => api.matches.seen(jobId),
    onSuccess: () => Promise.all([client.invalidateQueries({ queryKey: keys.overview }), client.invalidateQueries({ queryKey: keys.feedAll })]),
  });
}

export function useInterestedMutation() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (jobId: string) => api.matches.interested(jobId), onSuccess: () => invalidateMatchData(client) });
}

export function useDismissMutation() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (jobId: string) => api.matches.dismiss(jobId), onSuccess: () => invalidateMatchData(client) });
}

export function useCreateApplication() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: { jobId: string; status?: ApplicationStatus; notes?: string }) => api.applications.create(input),
    onSuccess: () => invalidateMatchData(client),
  });
}

export function usePatchApplication() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...patch }: { id: string; status?: ApplicationStatus; notes?: string; interviewDate?: string; salaryOffered?: number }) =>
      api.applications.patch(id, patch),
    onSuccess: () => invalidateMatchData(client),
  });
}

export function useRemoveApplication() {
  const client = useQueryClient();
  return useMutation({ mutationFn: (id: string) => api.applications.remove(id), onSuccess: () => invalidateMatchData(client) });
}

export function useUpdateProfile() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: ProfileInput) => api.profile.update(input),
    onSuccess: async (profile) => {
      client.setQueryData(keys.profile, profile);
      useAuthStore.setState((s) => (s.user ? { user: { ...s.user, fullName: profile.fullName } } : s));
      await invalidateMatchData(client);
    },
  });
}

export function useUpdatePreferences() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (input: PreferencesInput) => api.preferences.update(input),
    onSuccess: async (prefs) => {
      client.setQueryData(keys.preferences, prefs);
      await invalidateMatchData(client);
    },
  });
}

export function useAgentMessage() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => api.agent.send(text),
    onSuccess: async (reply) => {
      if (reply.changes.length > 0) {
        await client.invalidateQueries({ queryKey: keys.preferences });
        await invalidateMatchData(client);
      }
    },
  });
}

export function useResumes() {
  return useQuery({ queryKey: keys.resumes, queryFn: api.resumes.list });
}

export function useUploadResume() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (form: FormData) => api.resumes.upload(form),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.resumes }),
  });
}

export function useDeleteResume() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.resumes.remove(id),
    onSuccess: () => client.invalidateQueries({ queryKey: keys.resumes }),
  });
}

export function useClearHistory() {
  const client = useQueryClient();
  return useMutation({ mutationFn: api.account.clearHistory, onSuccess: () => invalidateMatchData(client) });
}
