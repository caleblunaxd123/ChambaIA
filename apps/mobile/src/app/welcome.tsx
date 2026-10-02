import { useRouter } from 'expo-router';
import { ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { BrandMark } from '@/features/auth/AuthLayout';
import { Button } from '@/ui/Button';
import { HeroSurface } from '@/ui/Card';
import { Icon, type IconName } from '@/ui/Icon';
import { SCREEN_MAX_WIDTH } from '@/ui/Screen';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

const PROMISES: { icon: IconName; title: string; text: string }[] = [
  { icon: 'document-text-outline', title: 'Sube tu CV una vez', text: 'Lo leemos por ti y tú revisas todo antes de guardarlo.' },
  { icon: 'funnel-outline', title: 'Solo lo que encaja', text: 'Descartamos duplicados y lo que no va con tu sueldo, distrito u horario.' },
  { icon: 'chatbubble-ellipses-outline', title: 'Te explica el porqué', text: 'Cada oferta trae sus motivos y sus riesgos. Sin porcentajes engañosos.' },
];

/** First screen for signed-out people: what the product does, in 10 seconds, and two clear ways in. */
export default function WelcomeScreen() {
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { colors, spacing } = useTheme();

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <ScrollView bounces={false} contentContainerStyle={{ flexGrow: 1 }} showsVerticalScrollIndicator={false}>
        <HeroSurface rounded={false} style={{ paddingTop: insets.top + spacing.xl, paddingBottom: spacing.xxxl, borderBottomLeftRadius: 32, borderBottomRightRadius: 32 }}>
          <View style={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', paddingHorizontal: spacing.xl, gap: spacing.xl }}>
            <BrandMark onHero />
            <View style={{ gap: 10 }}>
              <Text variant="display" tone="onHero" style={{ fontSize: 34, lineHeight: 40 }} testID="welcome-title">
                Tu agente personal para encontrar chamba
              </Text>
              <Text tone="onHero" style={{ opacity: 0.88, fontSize: 16, lineHeight: 24 }}>
                Busca ofertas por ti, te dice cuáles encajan contigo y te avisa sin llenarte de spam.
              </Text>
            </View>
            <PreviewCard />
          </View>
        </HeroSurface>

        <View style={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.xl, gap: spacing.lg, flex: 1 }}>
          {PROMISES.map((p) => (
            <View key={p.title} style={{ flexDirection: 'row', gap: 14, alignItems: 'flex-start' }}>
              <View style={{ width: 44, height: 44, borderRadius: 14, backgroundColor: colors.primaryTint, alignItems: 'center', justifyContent: 'center' }}>
                <Icon name={p.icon} size={22} tone="primary" />
              </View>
              <View style={{ flex: 1, gap: 2 }}>
                <Text variant="bodyStrong">{p.title}</Text>
                <Text variant="caption" tone="muted">{p.text}</Text>
              </View>
            </View>
          ))}
        </View>
      </ScrollView>

      <View style={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', paddingHorizontal: spacing.xl, paddingTop: spacing.md, paddingBottom: insets.bottom + spacing.lg, gap: 10 }}>
        <Button label="Crear mi cuenta gratis" trailingIcon="arrow-forward" onPress={() => router.push('/register')} fullWidth testID="welcome-register" />
        <Button label="Ya tengo cuenta" variant="ghost" onPress={() => router.push('/login')} fullWidth testID="welcome-login" />
      </View>
    </View>
  );
}

/** A tiny fake notification: shows the promise instead of describing it. */
function PreviewCard() {
  const { colors, radius, shadow } = useTheme();
  return (
    <View style={{ backgroundColor: colors.surface, borderRadius: radius.lg, padding: 14, gap: 10, ...shadow.raised }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
        <View style={{ width: 32, height: 32, borderRadius: 10, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' }}>
          <Icon name="sparkles" size={17} tone="onPrimary" />
        </View>
        <View style={{ flex: 1 }}>
          <Text variant="caption" style={{ fontFamily: fontFamily.bold }}>Tu agente · ahora</Text>
          <Text variant="caption" tone="muted">Encontré 7 oportunidades nuevas</Text>
        </View>
      </View>
      <View style={{ flexDirection: 'row', gap: 6, flexWrap: 'wrap' }}>
        <Pill color={colors.success} bg={colors.successTint} text="3 encajan muy bien" />
        <Pill color={colors.info} bg={colors.infoTint} text="2 posibles" />
        <Pill color={colors.textMuted} bg={colors.surfaceMuted} text="2 no te convienen" />
      </View>
    </View>
  );
}

function Pill({ color, bg, text }: { color: string; bg: string; text: string }) {
  return (
    <View style={{ paddingHorizontal: 10, paddingVertical: 4, borderRadius: 999, backgroundColor: bg }}>
      <Text variant="caption" style={{ color, fontSize: 12, fontFamily: fontFamily.semibold }}>{text}</Text>
    </View>
  );
}
