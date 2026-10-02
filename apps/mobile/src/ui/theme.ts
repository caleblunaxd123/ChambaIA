import { useColorScheme } from 'react-native';

import { useAppearance } from '@/state/appearance-store';

/**
 * ChambaIA design tokens. Every screen reads colours through `useTheme()`, so light and dark are just two objects
 * of the same `ThemeColors` shape. Never hard-code a colour in a component: add a token here instead.
 */

export type ThemeColors = {
  bg: string;
  surface: string;
  surfaceMuted: string;
  surfaceRaised: string;
  border: string;
  borderStrong: string;
  text: string;
  textMuted: string;
  textSubtle: string;
  primary: string;
  primaryPressed: string;
  primaryTint: string;
  onPrimary: string;
  accent: string;
  accentTint: string;
  success: string;
  successTint: string;
  warning: string;
  warningTint: string;
  danger: string;
  dangerPressed: string;
  dangerTint: string;
  info: string;
  infoTint: string;
  overlay: string;
  skeleton: string;
  /** Brand gradient used by hero cards, the welcome screen and the agent avatar. */
  heroFrom: string;
  heroTo: string;
  onHero: string;
  onHeroMuted: string;
  onHeroTint: string;
  /** Second data colour on the gradient (the "possible" segment). */
  onHeroAccent: string;
  /** "Live" dot on the agent status. */
  live: string;
  switchThumb: string;
};

export const lightColors: ThemeColors = {
  bg: '#F6F5F1',
  surface: '#FFFFFF',
  surfaceMuted: '#EFEDE7',
  surfaceRaised: '#FFFFFF',
  border: '#E6E2D9',
  borderStrong: '#D3CDC0',
  text: '#13203A',
  textMuted: '#556079',
  textSubtle: '#8790A3',
  primary: '#0B7A75',
  primaryPressed: '#08645F',
  primaryTint: '#DDF2EF',
  onPrimary: '#FFFFFF',
  accent: '#F2703A',
  accentTint: '#FFEDE2',
  success: '#0F8A54',
  successTint: '#DFF4E8',
  warning: '#A85F00',
  warningTint: '#FFF1D6',
  danger: '#C42B3A',
  dangerPressed: '#F7D3D8',
  dangerTint: '#FCE9EB',
  info: '#2A63D8',
  infoTint: '#E5EEFD',
  overlay: 'rgba(12, 20, 33, 0.48)',
  skeleton: '#E4E0D6',
  heroFrom: '#07514E',
  heroTo: '#0E8C83',
  onHero: '#FFFFFF',
  onHeroMuted: 'rgba(255, 255, 255, 0.82)',
  onHeroTint: 'rgba(255, 255, 255, 0.16)',
  onHeroAccent: '#FFD27A',
  live: '#7CF0B6',
  switchThumb: '#FFFFFF',
};

export const darkColors: ThemeColors = {
  bg: '#0B1215',
  surface: '#131C20',
  surfaceMuted: '#1B262B',
  surfaceRaised: '#18232A',
  border: '#24333A',
  borderStrong: '#34464E',
  text: '#ECF2F1',
  textMuted: '#A5B3B6',
  textSubtle: '#728389',
  primary: '#3CC2B5',
  primaryPressed: '#30A99D',
  primaryTint: '#123432',
  onPrimary: '#042220',
  accent: '#FF8D5C',
  accentTint: '#3A2318',
  success: '#45D493',
  successTint: '#11301F',
  warning: '#F2B544',
  warningTint: '#33280F',
  danger: '#FF6B76',
  dangerPressed: '#4A1F24',
  dangerTint: '#3A1A1E',
  info: '#79A9FF',
  infoTint: '#16243F',
  overlay: 'rgba(0, 0, 0, 0.62)',
  skeleton: '#223036',
  heroFrom: '#063B39',
  heroTo: '#0B6F68',
  onHero: '#FFFFFF',
  onHeroMuted: 'rgba(255, 255, 255, 0.8)',
  onHeroTint: 'rgba(255, 255, 255, 0.14)',
  onHeroAccent: '#FFD27A',
  live: '#7CF0B6',
  switchThumb: '#F4F7F7',
};

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;

export const radius = { sm: 10, md: 14, lg: 20, xl: 28, pill: 999 } as const;

type Shadows = { card: { boxShadow?: string }; raised: { boxShadow?: string }; soft: { boxShadow?: string } };

const lightShadow: Shadows = {
  card: { boxShadow: '0 1px 2px rgba(19, 32, 58, 0.04), 0 4px 16px rgba(19, 32, 58, 0.06)' },
  raised: { boxShadow: '0 12px 32px rgba(19, 32, 58, 0.18)' },
  soft: { boxShadow: '0 1px 6px rgba(19, 32, 58, 0.12)' },
};

// Shadows disappear on dark backgrounds; borders do the work there.
const darkShadow: Shadows = {
  card: {},
  raised: { boxShadow: '0 12px 32px rgba(0, 0, 0, 0.5)' },
  soft: { boxShadow: '0 1px 6px rgba(0, 0, 0, 0.4)' },
};

export const fontFamily = {
  regular: 'PlusJakartaSans_400Regular',
  medium: 'PlusJakartaSans_500Medium',
  semibold: 'PlusJakartaSans_600SemiBold',
  bold: 'PlusJakartaSans_700Bold',
  extrabold: 'PlusJakartaSans_800ExtraBold',
} as const;

export const typography = {
  display: { fontFamily: fontFamily.extrabold, fontSize: 30, lineHeight: 36, letterSpacing: -0.6 },
  title: { fontFamily: fontFamily.bold, fontSize: 22, lineHeight: 28, letterSpacing: -0.3 },
  heading: { fontFamily: fontFamily.bold, fontSize: 17, lineHeight: 23, letterSpacing: -0.1 },
  body: { fontFamily: fontFamily.regular, fontSize: 15, lineHeight: 22 },
  bodyStrong: { fontFamily: fontFamily.semibold, fontSize: 15, lineHeight: 22 },
  caption: { fontFamily: fontFamily.medium, fontSize: 13, lineHeight: 18 },
  label: { fontFamily: fontFamily.bold, fontSize: 11, lineHeight: 14, letterSpacing: 0.8 },
} as const;

export type TextVariant = keyof typeof typography;
export type TextTone = 'default' | 'muted' | 'subtle' | 'primary' | 'onPrimary' | 'onHero' | 'success' | 'warning' | 'danger' | 'info' | 'accent';

export type ColorScheme = 'light' | 'dark';

export type Theme = {
  scheme: ColorScheme;
  colors: ThemeColors;
  spacing: typeof spacing;
  radius: typeof radius;
  shadow: Shadows;
  typography: typeof typography;
};

export const lightTheme: Theme = { scheme: 'light', colors: lightColors, spacing, radius, shadow: lightShadow, typography };
export const darkTheme: Theme = { scheme: 'dark', colors: darkColors, spacing, radius, shadow: darkShadow, typography };

/** Resolves the user's choice ("Sistema" follows the phone) to a concrete scheme. */
export function resolveScheme(preference: 'system' | ColorScheme, system: string | null | undefined): ColorScheme {
  if (preference !== 'system') return preference;
  return system === 'dark' ? 'dark' : 'light';
}

export function useTheme(): Theme {
  const system = useColorScheme();
  const preference = useAppearance((s) => s.preference);
  return resolveScheme(preference, system) === 'dark' ? darkTheme : lightTheme;
}
