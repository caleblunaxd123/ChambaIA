import { z } from 'zod';

/**
 * Runtime contract with the API. Every response is parsed with these schemas, so a backend change that
 * breaks the app fails loudly in one place instead of rendering `undefined` in a screen.
 */

export const workModality = z.enum(['onSite', 'hybrid', 'remote']);
export const employmentType = z.enum(['fullTime', 'partTime', 'internship', 'temporary', 'freelance', 'other']);
export const educationLevel = z.enum(['secondary', 'technical', 'university', 'postgraduate']);
export const educationStatus = z.enum(['inProgress', 'completed']);
export const skillLevel = z.enum(['basic', 'intermediate', 'advanced']);
export const matchCategory = z.enum(['poor', 'review', 'compatible', 'veryCompatible', 'excellent']);
export const matchStatus = z.enum(['new', 'seen', 'interested', 'dismissed', 'applied', 'interview', 'offer', 'rejected']);
export const applicationStatus = z.enum(['found', 'interested', 'applied', 'interview', 'offer', 'discarded']);
export const notificationFrequency = z.enum(['instant', 'every2Hours', 'every6Hours', 'daily']);

export type WorkModality = z.infer<typeof workModality>;
export type EmploymentType = z.infer<typeof employmentType>;
export type EducationLevel = z.infer<typeof educationLevel>;
export type EducationStatus = z.infer<typeof educationStatus>;
export type SkillLevel = z.infer<typeof skillLevel>;
export type MatchCategory = z.infer<typeof matchCategory>;
export type MatchStatus = z.infer<typeof matchStatus>;
export type ApplicationStatus = z.infer<typeof applicationStatus>;
export type NotificationFrequency = z.infer<typeof notificationFrequency>;

// ---------- auth ----------

export const userSchema = z.object({ id: z.string(), email: z.string(), fullName: z.string(), plan: z.string() });
export type User = z.infer<typeof userSchema>;

export const authResponseSchema = z.object({
  accessToken: z.string(),
  accessTokenExpiresAt: z.string(),
  refreshToken: z.string(),
  user: userSchema,
});
export type AuthResponse = z.infer<typeof authResponseSchema>;

// ---------- profile & preferences ----------

export const skillSchema = z.object({ key: z.string(), name: z.string(), level: skillLevel });
export type Skill = z.infer<typeof skillSchema>;

export const profileSchema = z.object({
  fullName: z.string(),
  email: z.string(),
  headline: z.string().nullable(),
  experienceMonths: z.number(),
  educationLevel: educationLevel.nullable(),
  educationStatus: educationStatus.nullable(),
  skills: z.array(skillSchema),
  languages: z.array(z.object({ name: z.string(), level: z.string() })),
  experience: z.array(
    z.object({
      title: z.string(),
      company: z.string(),
      startDate: z.string().nullable(),
      endDate: z.string().nullable(),
      description: z.string().nullable(),
    }),
  ),
  education: z.array(z.object({ institution: z.string(), degree: z.string(), level: educationLevel, status: educationStatus })),
  certifications: z.array(z.string()),
  onboardingCompleted: z.boolean(),
  updatedAt: z.string(),
});
export type Profile = z.infer<typeof profileSchema>;

export const preferencesSchema = z.object({
  minSalary: z.number().nullable(),
  maxSalary: z.number().nullable(),
  preferredRoles: z.array(z.string()),
  excludedRoles: z.array(z.string()),
  excludedKeywords: z.array(z.string()),
  preferredIndustries: z.array(z.string()),
  preferredModalities: z.array(workModality),
  employmentTypes: z.array(employmentType),
  preferredDistricts: z.array(z.string()),
  excludedDistricts: z.array(z.string()),
  homeDistrict: z.string().nullable(),
  maxCommuteMinutes: z.number().nullable(),
  weekdaysOnly: z.boolean(),
  maxRequiredEducation: educationLevel.nullable(),
  notificationFrequency,
  pushEnabled: z.boolean(),
  updatedAt: z.string(),
});
export type Preferences = z.infer<typeof preferencesSchema>;

// ---------- jobs & matches ----------

export const jobSummarySchema = z.object({
  id: z.string(),
  title: z.string(),
  company: z.string(),
  district: z.string().nullable(),
  city: z.string(),
  modality: workModality,
  employmentType,
  salaryMin: z.number().nullable(),
  salaryMax: z.number().nullable(),
  salaryCurrency: z.string(),
  salaryLabel: z.string().nullable(),
  industry: z.string().nullable(),
  sourceName: z.string(),
  postedAt: z.string().nullable(),
});
export type JobSummary = z.infer<typeof jobSummarySchema>;

export const matchSummarySchema = z.object({
  category: matchCategory,
  categoryLabel: z.string(),
  status: matchStatus,
  topReasons: z.array(z.string()),
  topWarnings: z.array(z.string()),
});
export type MatchSummary = z.infer<typeof matchSummarySchema>;

export const feedItemSchema = z.object({ job: jobSummarySchema, match: matchSummarySchema.nullable() });
export type FeedItem = z.infer<typeof feedItemSchema>;

export const pagedSchema = <T extends z.ZodType>(item: T) =>
  z.object({ items: z.array(item), page: z.number(), pageSize: z.number(), total: z.number(), hasMore: z.boolean() });

export const feedPageSchema = pagedSchema(feedItemSchema);
export type FeedPage = z.infer<typeof feedPageSchema>;

const matchNoteSchema = z.object({ code: z.string(), title: z.string(), detail: z.string().nullable() });
export type MatchNote = z.infer<typeof matchNoteSchema>;

export const matchDetailSchema = z.object({
  category: matchCategory,
  categoryLabel: z.string(),
  recommendation: z.string(),
  status: matchStatus,
  matchedSkills: z.array(z.string()),
  missingSkills: z.array(z.string()),
  reasons: z.array(matchNoteSchema),
  warnings: z.array(matchNoteSchema),
  /** Per-axis explanation (role, skills, experience, conditions, meaning): qualitative levels, never percentages. */
  dimensions: z.array(
    z.object({ key: z.string(), label: z.string(), level: z.enum(['weak', 'medium', 'strong']), note: z.string() }),
  ),
  /** What-if suggestions computed by the matching engine. */
  improvements: z.array(z.object({ title: z.string(), detail: z.string(), resultCategory: matchCategory, resultLabel: z.string() })),
});
export type MatchDetail = z.infer<typeof matchDetailSchema>;

export const applicationSchema = z.object({
  id: z.string(),
  jobId: z.string(),
  status: applicationStatus,
  appliedAt: z.string().nullable(),
  notes: z.string().nullable(),
  interviewDate: z.string().nullable(),
  salaryOffered: z.number().nullable(),
  createdAt: z.string(),
  updatedAt: z.string(),
  job: jobSummarySchema.nullable(),
});
export type JobApplication = z.infer<typeof applicationSchema>;

const skillRequirementSchema = z.object({ key: z.string(), name: z.string(), minLevel: skillLevel.nullable() });

export const jobDetailResponseSchema = z.object({
  job: z.object({
    summary: jobSummarySchema,
    description: z.string(),
    schedule: z.string().nullable(),
    weekdaysOnly: z.boolean().nullable(),
    experienceRequiredMinMonths: z.number().nullable(),
    educationRequired: educationLevel.nullable(),
    educationRequiredCompleted: z.boolean(),
    skillsRequired: z.array(skillRequirementSchema),
    skillsPreferred: z.array(skillRequirementSchema),
    originalUrl: z.string(),
    expiresAt: z.string().nullable(),
  }),
  match: matchDetailSchema.nullable(),
  application: applicationSchema.nullable(),
});
export type JobDetailResponse = z.infer<typeof jobDetailResponseSchema>;

export const overviewSchema = z.object({
  newTotal: z.number(),
  newStrong: z.number(),
  newPossible: z.number(),
  newNotRecommended: z.number(),
  strong: z.number(),
  possible: z.number(),
  review: z.number(),
  notRecommended: z.number(),
  lastUpdatedAt: z.string().nullable(),
});
export type Overview = z.infer<typeof overviewSchema>;

// ---------- agent ----------

export const agentReplySchema = z.object({
  reply: z.string(),
  understood: z.boolean(),
  changes: z.array(z.string()),
  overview: overviewSchema.nullable(),
});
export type AgentReply = z.infer<typeof agentReplySchema>;

// ---------- resumes ----------

export const resumeSchema = z.object({
  id: z.string(),
  originalFilename: z.string(),
  mimeType: z.string(),
  sizeBytes: z.number(),
  createdAt: z.string(),
});
export type Resume = z.infer<typeof resumeSchema>;

/** What the parser proposes. The user always reviews and edits it before it reaches the profile. */
export const detectedResumeSchema = z.object({
  fullNameGuess: z.string().nullable(),
  headline: z.string().nullable(),
  experienceMonths: z.number(),
  educationLevel: educationLevel.nullable(),
  educationStatus: educationStatus.nullable(),
  skills: z.array(skillSchema),
  languages: z.array(z.object({ name: z.string(), level: z.string() })),
  experience: profileSchema.shape.experience,
  education: profileSchema.shape.education,
  certifications: z.array(z.string()),
  suggestedRoles: z.array(z.string()),
  warnings: z.array(z.string()),
});
export type DetectedResume = z.infer<typeof detectedResumeSchema>;

export const resumeDetailSchema = z.object({ resume: resumeSchema, detected: detectedResumeSchema.nullable() });
export type ResumeDetail = z.infer<typeof resumeDetailSchema>;

// ---------- sources ----------

export const jobSourceKind = z.enum(['demo', 'api', 'feed', 'careerPage', 'manual']);

export const sourceSchema = z.object({
  key: z.string(),
  name: z.string(),
  kind: jobSourceKind,
  lastFetchedAt: z.string().nullable(),
  activeOffers: z.number(),
  healthy: z.boolean(),
});
export type JobSourceInfo = z.infer<typeof sourceSchema>;

// ---------- notifications ----------

export const notificationKind = z.enum(['newJobs', 'test']);

export const notificationSchema = z.object({
  id: z.string(),
  kind: notificationKind,
  title: z.string(),
  body: z.string(),
  jobId: z.string().nullable(),
  matchCount: z.number(),
  strongCount: z.number(),
  createdAt: z.string(),
  readAt: z.string().nullable(),
});
export type AppNotification = z.infer<typeof notificationSchema>;

export const notificationPageSchema = pagedSchema(notificationSchema);

export const notificationSummarySchema = z.object({ unread: z.number(), activeDevices: z.number(), pushConfigured: z.boolean() });
export type NotificationSummary = z.infer<typeof notificationSummarySchema>;

export const testNotificationSchema = z.object({ devices: z.number(), accepted: z.number(), pushConfigured: z.boolean() });
export type TestNotificationResult = z.infer<typeof testNotificationSchema>;
