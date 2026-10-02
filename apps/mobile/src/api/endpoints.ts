import { z } from 'zod';

import { request } from './client';
import {
  type ApplicationStatus,
  type MatchCategory,
  type Preferences,
  type Profile,
  type WorkModality,
  agentReplySchema,
  resumeDetailSchema,
  sourceSchema,
  resumeSchema,
  applicationSchema,
  authResponseSchema,
  feedItemSchema,
  feedPageSchema,
  jobDetailResponseSchema,
  matchSummarySchema,
  notificationPageSchema,
  notificationSummarySchema,
  overviewSchema,
  preferencesSchema,
  profileSchema,
  testNotificationSchema,
} from './schemas';

export type FeedTab = 'forYou' | 'new' | 'saved';

export type FeedFilters = {
  q?: string;
  district?: string;
  modality?: WorkModality;
  minSalary?: number;
  postedWithinDays?: number;
  category?: MatchCategory;
  /** Undefined = the tab's natural order (relevance for "Para ti", newest for "Nuevas"). */
  sort?: FeedSort;
};

export type FeedSort = 'relevance' | 'recent' | 'salary';

export function buildQuery(params: Record<string, string | number | undefined | null>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && String(value).length > 0) search.set(key, String(value));
  }
  const text = search.toString();
  return text ? `?${text}` : '';
}

export const api = {
  auth: {
    register: (input: { email: string; password: string; fullName: string }) =>
      request('/auth/register', { method: 'POST', body: input, schema: authResponseSchema, auth: false }),
    login: (input: { email: string; password: string }) =>
      request('/auth/login', { method: 'POST', body: input, schema: authResponseSchema, auth: false }),
    logout: (refreshToken: string) => request('/auth/logout', { method: 'POST', body: { refreshToken } }),
  },

  resumes: {
    list: () => request('/resumes', { schema: z.array(resumeSchema) }),
    upload: (form: FormData) => request('/resumes', { method: 'POST', body: form, schema: resumeDetailSchema }),
    remove: (id: string) => request(`/resumes/${id}`, { method: 'DELETE' }),
  },

  account: {
    clearHistory: () => request('/account/clear-history', { method: 'POST' }),
    remove: (password: string) => request('/account/delete', { method: 'POST', body: { password } }),
  },

  profile: {
    get: () => request('/profile', { schema: profileSchema }),
    update: (profile: ProfileInput) => request('/profile', { method: 'PUT', body: profile, schema: profileSchema }),
  },

  preferences: {
    get: () => request('/preferences', { schema: preferencesSchema }),
    update: (prefs: PreferencesInput) => request('/preferences', { method: 'PUT', body: prefs, schema: preferencesSchema }),
  },

  matches: {
    overview: () => request('/matches/overview', { schema: overviewSchema }),
    feed: (tab: FeedTab, filters: FeedFilters, page: number, pageSize = 15) =>
      request(`/matches${buildQuery({ tab, ...filters, page, pageSize })}`, { schema: feedPageSchema }),
    refresh: () => request('/matches/refresh', { method: 'POST', schema: z.object({ evaluated: z.number() }) }),
    seen: (jobId: string) => request(`/matches/${jobId}/seen`, { method: 'POST' }),
    interested: (jobId: string) => request(`/matches/${jobId}/interested`, { method: 'POST', schema: matchSummarySchema }),
    dismiss: (jobId: string) => request(`/matches/${jobId}/dismiss`, { method: 'POST', schema: matchSummarySchema }),
    /** Undo for "me interesa" / "descartar": the offer goes back to the feed. */
    reset: (jobId: string) => request(`/matches/${jobId}/reset`, { method: 'POST', schema: matchSummarySchema }),
  },

  jobs: {
    detail: (id: string) => request(`/jobs/${id}`, { schema: jobDetailResponseSchema }),
    similar: (id: string, limit = 4) => request(`/jobs/${id}/similar?limit=${limit}`, { schema: z.array(feedItemSchema) }),
  },

  applications: {
    list: () => request('/applications', { schema: z.array(applicationSchema) }),
    create: (input: { jobId: string; status?: ApplicationStatus; notes?: string }) =>
      request('/applications', { method: 'POST', body: input, schema: applicationSchema }),
    patch: (id: string, input: ApplicationPatch) =>
      request(`/applications/${id}`, { method: 'PATCH', body: input, schema: applicationSchema }),
    remove: (id: string) => request(`/applications/${id}`, { method: 'DELETE' }),
  },

  sources: {
    list: () => request('/sources', { schema: z.array(sourceSchema) }),
  },

  catalog: {
    districts: () => request("/catalog/districts", { schema: z.array(z.string()) }),
    skills: () => request("/catalog/skills", { schema: z.array(z.object({ key: z.string(), name: z.string() })) }),
  },

  devices: {
    register: (token: string, platform: 'ios' | 'android') => request('/devices', { method: 'POST', body: { token, platform } }),
    unregister: (token: string) => request('/devices/unregister', { method: 'POST', body: { token } }),
  },

  notifications: {
    list: (page: number, pageSize = 20) => request(`/notifications${buildQuery({ page, pageSize })}`, { schema: notificationPageSchema }),
    summary: () => request('/notifications/summary', { schema: notificationSummarySchema }),
    read: (id: string) => request(`/notifications/${id}/read`, { method: 'POST' }),
    readAll: () => request('/notifications/read-all', { method: 'POST' }),
    test: () => request('/notifications/test', { method: 'POST', schema: testNotificationSchema }),
  },

  agent: {
    send: (text: string) => request('/agent/messages', { method: 'POST', body: { text }, schema: agentReplySchema }),
  },
};

export type ApplicationPatch = {
  status?: ApplicationStatus;
  notes?: string;
  interviewDate?: string;
  /** The API cannot tell "unchanged" from "remove" with null, so removing the date is explicit. */
  clearInterviewDate?: boolean;
  salaryOffered?: number;
};

/** What the profile PUT accepts (same shape as the response minus read-only fields). */
export type ProfileInput = Pick<
  Profile,
  'fullName' | 'headline' | 'experienceMonths' | 'educationLevel' | 'educationStatus' | 'skills' | 'languages' | 'experience' | 'education' | 'certifications'
> & { completeOnboarding?: boolean };

export type PreferencesInput = Omit<Preferences, 'updatedAt'>;
