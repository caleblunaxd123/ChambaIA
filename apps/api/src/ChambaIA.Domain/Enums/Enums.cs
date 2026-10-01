namespace ChambaIA.Domain.Enums;

public enum WorkModality { OnSite, Hybrid, Remote }

public enum EmploymentType { FullTime, PartTime, Internship, Temporary, Freelance, Other }

/// <summary>Ordered by seniority: comparisons (&gt;, &lt;) are meaningful.</summary>
public enum EducationLevel { Secondary = 1, Technical = 2, University = 3, Postgraduate = 4 }

public enum EducationStatus { InProgress, Completed }

public enum SkillLevel { Basic = 1, Intermediate = 2, Advanced = 3 }

public enum SubscriptionPlan { Free, Pro, ProPlus }

public enum NotificationFrequency { Instant, Every2Hours, Every6Hours, Daily }

public enum JobSourceKind { Demo, Api, Feed, CareerPage, Manual }

/// <summary>Ordered by quality: comparisons are meaningful.</summary>
public enum MatchCategory { Poor, Review, Compatible, VeryCompatible, Excellent }

public enum MatchStatus { New, Seen, Interested, Dismissed, Applied, Interview, Offer, Rejected }

public enum ApplicationStatus { Found, Interested, Applied, Interview, Offer, Discarded }
