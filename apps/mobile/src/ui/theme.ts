/**
 * ChambaIA design tokens. Light theme first; `ThemeColors` is the single shape a dark palette must satisfy,
 * so adding dark mode later means adding one object and switching it in `useTheme`.
 */

export type ThemeColors = {
  bg: string;
  surface: string;
  surfaceMuted: string;
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
  dangerTint: string;
  info: string;
  infoTint: string;
  overlay: string;
};

export const lightColors: ThemeColors = {
  bg: '#FBF9F6',
  surface: '#FFFFFF',
  surfaceMuted: '#F4F1EB',
  border: '#EBE6DD',
  borderStrong: '#D8D1C4',
  text: '#1B2437',
  textMuted: '#5B6578',
  textSubtle: '#8A93A5',
  primary: '#0B7A75',
  primaryPressed: '#096762',
  primaryTint: '#E0F3F1',
  onPrimary: '#FFFFFF',
  accent: '#FF8A3D',
  accentTint: '#FFF0E3',
  success: '#12945B',
  successTint: '#E4F6EC',
  warning: '#B86A06',
  warningTint: '#FFF3DD',
  danger: '#C8323F',
  dangerTint: '#FDECEE',
  info: '#2F6FEB',
  infoTint: '#E8F0FE',
  overlay: 'rgba(17, 24, 39, 0.45)',
};

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32, xxxl: 48 } as const;

export const radius = { sm: 10, md: 14, lg: 20, xl: 28, pill: 999 } as const;

export const shadow = {
  card: { boxShadow: '0 2px 14px rgba(27, 36, 55, 0.07)' },
  raised: { boxShadow: '0 8px 28px rgba(27, 36, 55, 0.14)' },
} as const;

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
export type TextTone = 'default' | 'muted' | 'subtle' | 'primary' | 'onPrimary' | 'success' | 'warning' | 'danger';

export type Theme = {
  colors: ThemeColors;
  spacing: typeof spacing;
  radius: typeof radius;
  shadow: typeof shadow;
  typography: typeof typography;
};

export const lightTheme: Theme = { colors: lightColors, spacing, radius, shadow, typography };

/** Hook-shaped on purpose: when dark mode lands, only this function changes. */
export function useTheme(): Theme {
  return lightTheme;
}
