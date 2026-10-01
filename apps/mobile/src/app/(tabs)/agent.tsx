import { useEffect, useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useAgentMessage, useOverview, usePreferences, useUpdatePreferences } from '@/api/queries';
import { Remembered } from '@/features/agent/Remembered';
import { formatRelativeTime } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { type ChatMessage, useAgentChat } from '@/state/agent-chat';
import { Card } from '@/ui/Card';
import { Chip } from '@/ui/Chip';
import { Icon } from '@/ui/Icon';
import { SCREEN_MAX_WIDTH } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

const SUGGESTIONS = ['Mínimo 1800 soles', 'No me muestres trabajos en Ate', 'Busca también facturación', 'Máximo una hora de viaje', 'Solo lunes a viernes', 'No quiero call center'];

export default function AgentScreen() {
  const { colors, spacing, radius } = useTheme();
  const insets = useSafeAreaInsets();
  const overview = useOverview();
  const prefs = usePreferences();
  const updatePrefs = useUpdatePreferences();
  const send = useAgentMessage();
  const { messages, push } = useAgentChat();
  const [text, setText] = useState('');
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

  return (
    <KeyboardAvoidingView style={{ flex: 1, backgroundColor: colors.bg }} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <ScrollView
        ref={scroller}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{ width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center', padding: spacing.lg, paddingTop: insets.top + spacing.md, gap: spacing.lg }}
      >
        <View style={{ gap: 4 }}>
          <Text variant="display">Tu agente</Text>
          <Text tone="muted">Dile lo que quieres y lo recordará. Trabaja por ti aunque cierres la app.</Text>
        </View>

        <Card tone="brand" style={{ gap: 10 }} testID="agent-status">
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
            <View style={{ width: 10, height: 10, borderRadius: 5, backgroundColor: '#7CF0B6' }} />
            <Text variant="bodyStrong" tone="onPrimary">Activo y buscando</Text>
          </View>
          {overview.isLoading ? (
            <Skeleton width="70%" style={{ backgroundColor: 'rgba(255,255,255,0.35)' }} />
          ) : (
            <Text tone="onPrimary" style={{ opacity: 0.92 }}>
              {o ? `${o.strong} muy compatibles · ${o.possible + o.review} para revisar` : 'Aún sin resultados'}
              {o?.lastUpdatedAt ? ` · actualizado ${formatRelativeTime(o.lastUpdatedAt)}` : ''}
            </Text>
          )}
        </Card>

        <View style={{ gap: 10 }}>
          <Text variant="title">Lo que recuerda tu agente</Text>
          {prefs.isLoading ? <Skeleton height={64} radius={16} /> : prefs.data ? (
            <Remembered prefs={prefs.data} disabled={updatePrefs.isPending} onChange={(next) => updatePrefs.mutate(next, { onSuccess: () => haptics.tap() })} />
          ) : null}
        </View>

        <View style={{ gap: 12 }}>
          <Text variant="title">Conversación</Text>
          {messages.map((m) => (
            <Bubble key={m.id} message={m} />
          ))}
          {send.isPending ? <Typing /> : null}
        </View>
      </ScrollView>

      <View style={{ borderTopWidth: 1, borderTopColor: colors.border, backgroundColor: colors.surface, paddingTop: 10, paddingBottom: 10 }}>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ paddingHorizontal: spacing.lg, gap: 8 }} keyboardShouldPersistTaps="handled" style={{ flexGrow: 0 }}>
          {SUGGESTIONS.map((s) => (
            <Chip key={s} label={s} onPress={() => submit(s)} />
          ))}
        </ScrollView>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10, paddingHorizontal: spacing.lg, paddingTop: 10, width: '100%', maxWidth: SCREEN_MAX_WIDTH, alignSelf: 'center' }}>
          <TextInput
            testID="agent-input"
            value={text}
            onChangeText={setText}
            onSubmitEditing={() => submit(text)}
            placeholder="Escribe lo que buscas o lo que no quieres…"
            placeholderTextColor={colors.textSubtle}
            returnKeyType="send"
            maxLength={500}
            style={{ flex: 1, minHeight: 46, borderRadius: radius.pill, borderWidth: 1.5, borderColor: colors.border, backgroundColor: colors.bg, paddingHorizontal: 16, color: colors.text, fontFamily: fontFamily.medium, fontSize: 15, outlineStyle: 'solid', outlineWidth: 0 }}
          />
          <Pressable
            testID="agent-send"
            accessibilityRole="button"
            accessibilityLabel="Enviar"
            disabled={!text.trim() || send.isPending}
            onPress={() => submit(text)}
            style={{ width: 46, height: 46, borderRadius: 23, backgroundColor: text.trim() ? colors.primary : colors.borderStrong, alignItems: 'center', justifyContent: 'center' }}
          >
            <Icon name="arrow-up" size={22} tone="onPrimary" />
          </Pressable>
        </View>
      </View>
    </KeyboardAvoidingView>
  );
}

function Bubble({ message }: { message: ChatMessage }) {
  const { colors, radius } = useTheme();
  const mine = message.from === 'user';
  return (
    <View style={{ alignItems: mine ? 'flex-end' : 'flex-start', gap: 6 }} testID={`msg-${message.from}`}>
      <View
        style={{
          maxWidth: '88%',
          paddingHorizontal: 14,
          paddingVertical: 10,
          borderRadius: radius.lg,
          borderBottomRightRadius: mine ? 6 : radius.lg,
          borderBottomLeftRadius: mine ? radius.lg : 6,
          backgroundColor: mine ? colors.primary : message.failed ? colors.dangerTint : colors.surface,
          borderWidth: mine ? 0 : 1,
          borderColor: colors.border,
        }}
      >
        <Text tone={mine ? 'onPrimary' : message.failed ? 'danger' : 'default'}>{message.text}</Text>
      </View>
    </View>
  );
}

function Typing() {
  const { colors, radius } = useTheme();
  return (
    <View style={{ alignSelf: 'flex-start', flexDirection: 'row', gap: 5, paddingHorizontal: 16, paddingVertical: 14, borderRadius: radius.lg, backgroundColor: colors.surface, borderWidth: 1, borderColor: colors.border }} testID="agent-typing">
      {[0, 1, 2].map((i) => (
        <View key={i} style={{ width: 7, height: 7, borderRadius: 4, backgroundColor: colors.textSubtle, opacity: 0.5 + i * 0.2 }} />
      ))}
    </View>
  );
}
