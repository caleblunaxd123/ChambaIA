import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { Animated, KeyboardAvoidingView, Platform, Pressable, ScrollView, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAgentMessage, useOverview, usePreferences, useUpdatePreferences } from '@/api/queries';
import { Remembered, rememberedCount } from '@/features/agent/Remembered';
import { jobsLink } from '@/features/jobs/links';
import { formatRelativeTime } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { type ChatMessage, useAgentChat } from '@/state/agent-chat';
import { toast } from '@/state/toast-store';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Chip } from '@/ui/Chip';
import { Icon } from '@/ui/Icon';
import { SCREEN_MAX_WIDTH } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';


const SUGGESTIONS = ['Mínimo 1800 soles', 'No me muestres trabajos en Ate', 'Busca también facturación', 'Máximo una hora de viaje', 'Solo lunes a viernes', 'No quiero call center'];

export default function AgentScreen() {
  const router = useRouter();
  const { colors, spacing, radius } = useTheme();
  const insets = useSafeAreaInsets();
  const overview = useOverview();
  const prefs = usePreferences();
  const updatePrefs = useUpdatePreferences();
  const send = useAgentMessage();
  const { messages, push } = useAgentChat();
  const [text, setText] = useState('');
  const [memoryOpen, setMemoryOpen] = useState(false);
  const scroller = useRef<ScrollView>(null);

  useEffect(() => {
    const id = setTimeout(() => scroller.current?.scrollToEnd({ animated: true }), 80);
    return () => clearTimeout(id);
  }, [messages.length, send.isPending]);

  const submit = (raw: string) => {
    const message = raw.trim();
    if (!message || send.isPending) return;
    setText('');
    haptics.tap();
    push({ from: 'user', text: message });
    send.mutate(message, {
      onSuccess: (reply) => {
        push({ from: 'agent', text: reply.reply, changes: reply.changes });
        if (reply.changes.length > 0) haptics.success();
        else haptics.warning();
      },
      onError: (error) => {
        push({ from: 'agent', text: error.message, failed: true });
        haptics.error();
      },
    });
  };

  const o = overview.data;
  const remembered = prefs.data ? rememberedCount(prefs.data) : 0;
  const used = new Set(messages.filter((m) => m.from === 'user').map((m) => m.text));
  const suggestions = SUGGESTIONS.filter((s) => !used.has(s));

  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={{ paddingTop: insets.top + 10, paddingBottom: 12, paddingHorizontal: spacing.lg, backgroundColor: colors.surface, borderBottomWidth: 1, borderBottomColor: colors.border }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12, width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
          <AgentAvatar size={46} />
          <View style={{ flex: 1, gap: 1 }} testID="agent-status">
            <Text variant="heading">Tu agente</Text>
            {overview.isLoading ? (
              <Skeleton width="70%" height={12} />
            ) : (
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                <View style={{ width: 7, height: 7, borderRadius: 4, backgroundColor: colors.success }} />
                <Text variant="caption" tone="muted" numberOfLines={1} style={{ flex: 1 }}>
                  {o ? `Activo · ${o.strong} muy compatibles${o.lastUpdatedAt ? ` · ${formatRelativeTime(o.lastUpdatedAt)}` : ''}` : 'Activo y buscando'}
                </Text>
              </View>
            )}
          </View>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={`Lo que recuerdo: ${remembered} preferencias`}
            testID="agent-memory"
            onPress={() => {
              haptics.tap();
              setMemoryOpen(true);
            }}
            style={({ pressed }) => ({ flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 8, borderRadius: radius.pill, backgroundColor: pressed ? colors.border : colors.primaryTint })}
          >
            <Icon name="bulb-outline" size={16} tone="primary" />
            <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>Recuerdo {remembered}</Text>
          </Pressable>
        </View>
      </View>

      <ScrollView
        ref={scroller}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, gap: 14 }}
      >
        <Text variant="caption" tone="subtle" style={{ textAlign: 'center' }}>Dile lo que quieres y lo recordará. Trabaja por ti aunque cierres la app.</Text>
        {messages.map((m) => (
          <Bubble key={m.id} message={m} onSeeJobs={() => router.navigate(jobsLink('forYou'))} />
        ))}
        {send.isPending ? <Typing /> : null}
      </ScrollView>

      <View style={{ borderTopWidth: 1, borderTopColor: colors.border, backgroundColor: colors.surface, paddingTop: 10, paddingBottom: 10 }}>
        {text.trim().length === 0 && suggestions.length > 0 ? (
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: spacing.lg, gap: 8 }} keyboardShouldPersistTaps="handled" style={{ flexGrow: 0, marginBottom: 10 }}>
            {suggestions.map((s) => (
              <Chip key={s} label={s} icon="flash-outline" onPress={() => submit(s)} />
            ))}
          </ScrollView>
        ) : null}
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: spacing.lg, width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
          <TextInput
            testID="agent-input"
            value={text}
            onChangeText={setText}
            onSubmitEditing={() => submit(text)}
            placeholder="Escribe lo que buscas o lo que no quieres…"
            placeholderTextColor={colors.textSubtle}
            returnKeyType="send"
            maxLength={500}
            style={{ flex: 1, minHeight: 48, borderRadius: radius.pill, borderWidth: 1.5, borderColor: colors.border, backgroundColor: colors.bg, paddingHorizontal: 18, color: colors.text, fontFamily: fontFamily.medium, fontSize: 15, outlineStyle: 'solid', outlineWidth: 0 }}
          />
          <Pressable
            testID="agent-send"
            accessibilityRole="button"
            accessibilityLabel="Enviar"
            disabled={!text.trim() || send.isPending}
            onPress={() => submit(text)}
            style={({ pressed }) => ({ width: 48, height: 48, borderRadius: 24, backgroundColor: text.trim() ? (pressed ? colors.primaryPressed : colors.primary) : colors.surfaceMuted, alignItems: 'center', justifyContent: 'center' })}
          >
            <Icon name="arrow-up" size={22} color={text.trim() ? colors.onPrimary : colors.textSubtle} />
          </Pressable>
        </View>
      </View>

      <BottomSheet visible={memoryOpen} onClose={() => setMemoryOpen(false)} title="Lo que recuerdo de ti" footer={<Button label="Editar todas mis preferencias" variant="secondary" icon="options-outline" onPress={() => { setMemoryOpen(false); router.push('/edit-preferences'); }} fullWidth />}>
        <Text tone="muted">Toca la × para que lo olvide. Tus ofertas se actualizan al instante.</Text>
        {prefs.isLoading ? (
          <Skeleton height={64} radius={16} />
        ) : prefs.data ? (
          <Remembered
            prefs={prefs.data}
            disabled={updatePrefs.isPending}
            onChange={(next) =>
              updatePrefs.mutate(next, {
                onSuccess: () => {
                  haptics.tap();
                  toast.show({ message: 'Listo, lo olvidé. Actualicé tus ofertas.', tone: 'success' });
                },
                onError: (error) => toast.show({ message: error.message, tone: 'danger' }),
              })
            }
          />
        ) : null}
      </BottomSheet>
    </KeyboardAvoidingView>
  );
}

function AgentAvatar({ size }: { size: number }) {
  const { colors } = useTheme();
  return (
    <LinearGradient colors={[colors.heroFrom, colors.heroTo]} start={{ x: 0, y: 0 }} end={{ x: 1, y: 1 }} style={{ width: size, height: size, borderRadius: size / 2, alignItems: 'center', justifyContent: 'center' }}>
      <Icon name="sparkles" size={size * 0.48} tone="onHero" />
    </LinearGradient>
  );
}

function Bubble({ message, onSeeJobs }: { message: ChatMessage; onSeeJobs: () => void }) {
  const { colors, radius } = useTheme();
  const mine = message.from === 'user';
  const changes = message.changes ?? [];
  return (
    <View style={{ flexDirection: 'row', justifyContent: mine ? 'flex-end' : 'flex-start', alignItems: 'flex-end', gap: 8 }} testID={`msg-${message.from}`}>
      {!mine ? <AgentAvatar size={28} /> : null}
      <View style={{ maxWidth: '84%', gap: 8 }}>
        <View
          style={{
            paddingHorizontal: 14,
            paddingVertical: 10,
            borderRadius: radius.lg,
            borderBottomRightRadius: mine ? 6 : radius.lg,
            borderBottomLeftRadius: mine ? radius.lg : 6,
            backgroundColor: mine ? colors.primary : message.failed ? colors.dangerTint : colors.surface,
            borderWidth: mine ? 0 : 1,
            borderColor: message.failed ? colors.dangerTint : colors.border,
          }}
        >
          <Text tone={mine ? 'onPrimary' : message.failed ? 'danger' : 'default'}>{message.text}</Text>
        </View>
        {changes.length > 0 ? (
          <View style={{ padding: 12, gap: 8, borderRadius: radius.md, backgroundColor: colors.successTint }} testID="agent-changes">
            <Text variant="label" tone="success">Actualicé tu búsqueda</Text>
            {changes.map((c) => (
              <View key={c} style={{ flexDirection: 'row', gap: 8, alignItems: 'flex-start' }}>
                <Icon name="checkmark-circle" size={16} tone="success" />
                <Text variant="caption" style={{ flex: 1 }}>{c}</Text>
              </View>
            ))}
            <Pressable onPress={onSeeJobs} accessibilityRole="button" hitSlop={6} style={{ flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 2 }}>
              <Text variant="caption" tone="primary" style={{ fontFamily: fontFamily.bold }}>Ver mis ofertas actualizadas</Text>
              <Icon name="arrow-forward" size={14} tone="primary" />
            </Pressable>
          </View>
        ) : null}
      </View>
    </View>
  );
}

function Typing() {
  const { colors, radius } = useTheme();
  const [progress] = useState(() => new Animated.Value(0));

  useEffect(() => {
    const loop = Animated.loop(Animated.timing(progress, { toValue: 1, duration: 1100, useNativeDriver: true }));
    loop.start();
    return () => loop.stop();
  }, [progress]);

  return (
    <View style={{ flexDirection: 'row', alignItems: 'flex-end', gap: 8 }} testID="agent-typing">
      <AgentAvatar size={28} />
      <View accessibilityLabel="Tu agente está escribiendo" style={{ flexDirection: 'row', gap: 5, paddingHorizontal: 16, paddingVertical: 15, borderRadius: radius.lg, borderBottomLeftRadius: 6, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border }}>
        {[0, 1, 2].map((i) => (
          <Animated.View
            key={i}
            style={{
              width: 7,
              height: 7,
              borderRadius: 4,
              backgroundColor: colors.textSubtle,
              opacity: progress.interpolate({ inputRange: [0, (i + 1) / 4, (i + 2) / 4, 1], outputRange: [0.3, 1, 0.3, 0.3] }),
            }}
          />
        ))}
      </View>
    </View>
  );
}
