import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { useClearHistory, useDeleteResume, usePreferences, useProfile, useResumes } from '@/api/queries';
import { logout, useDeleteAccount } from '@/features/auth/useAuthActions';
import { SourcesSheet } from '@/features/profile/SourcesSheet';
import { educationLabel, formatDuration, formatMoney, formatRelativeTime, frequencyLabel, modalityLabel, skillLevelLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { profileStrength } from '@/lib/profile-strength';
import { type AppearancePreference, useAppearance } from '@/state/appearance-store';
import { useAuthStore } from '@/state/auth-store';
import { toast } from '@/state/toast-store';
import { Avatar } from '@/ui/Avatar';
import { Badge } from '@/ui/Badge';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { SegmentedTabs } from '@/ui/Chip';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { ErrorState, InlineError } from '@/ui/EmptyState';
import { Icon } from '@/ui/Icon';
import { Input } from '@/ui/Input';
import { ListGroup, ListRow } from '@/ui/ListRow';
import { ProgressBar } from '@/ui/ProgressBar';
import { Screen } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { fontFamily, useTheme } from '@/ui/theme';

const SKILLS_PREVIEW = 8;

export default function ProfileScreen() {
  const router = useRouter();
  const { colors } = useTheme();
  const user = useAuthStore((s) => s.user);
  const profile = useProfile();
  const prefs = usePreferences();
  const resumes = useResumes();
  const appearance = useAppearance((s) => s.preference);
  const setAppearance = useAppearance((s) => s.setPreference);
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [password, setPassword] = useState('');
  const deleteAccount = useDeleteAccount();
  const deleteResume = useDeleteResume();
  const clearHistory = useClearHistory();
  const [confirmCv, setConfirmCv] = useState(false);
  const [confirmHistory, setConfirmHistory] = useState(false);
  const [sourcesOpen, setSourcesOpen] = useState(false);
  const cv = resumes.data?.[0];

  const p = profile.data;
  const pr = prefs.data;
  const strength = profileStrength(p, pr, cv !== undefined);
  const education = p?.educationLevel ? `${educationLabel[p.educationLevel]}${p.educationStatus === 'inProgress' ? ' (en curso)' : p.educationStatus === 'completed' ? ' (terminado)' : ''}` : null;
  const uploadCv = () => router.push({ pathname: '/onboarding', params: { mode: 'cv' } });

  const profileSummary = p
    ? [p.experienceMonths > 0 ? `${formatDuration(p.experienceMonths)} de experiencia` : null, education].filter(Boolean).join(' · ') || 'Completa tu experiencia y estudios'
    : '';
  const prefsSummary = pr
    ? [
        pr.minSalary != null ? `Desde ${formatMoney(pr.minSalary)}` : null,
        pr.homeDistrict ? `Vivo en ${pr.homeDistrict}` : null,
        pr.preferredModalities.length > 0 ? pr.preferredModalities.map((m) => modalityLabel[m]).join(', ') : null,
      ].filter(Boolean).join(' · ') || 'Sueldo, distrito, horario y más'
    : '';

  return (
    <Screen onRefresh={() => { void profile.refetch(); void prefs.refetch(); void resumes.refetch(); }} refreshing={profile.isRefetching}>
      <Card style={{ gap: 16 }} testID="profile-header">
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 14 }}>
          <Avatar name={user?.fullName ?? '?'} size={64} shape="circle" />
          <View style={{ flex: 1, gap: 2 }}>
            <Text variant="title" numberOfLines={1} testID="profile-name">{user?.fullName}</Text>
            <Text variant="caption" tone="muted" numberOfLines={1}>{user?.email}</Text>
            <View style={{ marginTop: 4 }}><Badge label="Plan Gratis" tone="brand" icon="sparkles" size="sm" /></View>
          </View>
        </View>
        {p && pr ? (
          <View style={{ gap: 8 }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
              <Text variant="caption" tone="muted">Qué tanto te conoce tu agente</Text>
              <Text variant="caption" style={{ fontFamily: fontFamily.bold }}>{strength.percent}%</Text>
            </View>
            <ProgressBar value={strength.value} color={strength.value >= 1 ? colors.success : colors.primary} />
            {strength.next ? (
              <Button label={strength.next.label} icon="add-circle-outline" variant="tonal" size="sm" onPress={() => (strength.next?.key === 'cv' ? uploadCv() : router.push(strength.next?.route ?? '/edit-profile'))} testID="strength-next" />
            ) : (
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                <Icon name="checkmark-circle" size={16} tone="success" />
                <Text variant="caption" tone="success">Perfil completo: tu agente tiene todo para encontrarte lo mejor.</Text>
              </View>
            )}
          </View>
        ) : (
          <Skeleton height={8} />
        )}
      </Card>

      {profile.isError ? <ErrorState message={profile.error.message} onRetry={() => void profile.refetch()} /> : null}

      <ListGroup title="Tu información">
        <ListRow icon="person-outline" title="Mi perfil" subtitle={profile.isLoading ? 'Cargando…' : (p?.headline ?? profileSummary)} onPress={() => router.push('/edit-profile')} testID="profile-card" />
        <ListRow icon="options-outline" title="Lo que busco" subtitle={prefs.isLoading ? 'Cargando…' : prefsSummary} onPress={() => router.push('/edit-preferences')} testID="preferences-card" />
        <ListRow
          icon="document-text-outline"
          title={cv ? 'Mi CV' : 'Subir mi CV'}
          subtitle={resumes.isLoading ? 'Cargando…' : cv ? `${cv.originalFilename} · subido ${formatRelativeTime(cv.createdAt)}` : 'Completamos tu perfil por ti. Tú revisas todo.'}
          onPress={uploadCv}
          testID={cv ? 'cv-card' : 'cv-upload'}
        />
      </ListGroup>

      {p && p.skills.length > 0 ? (
        <Card style={{ gap: 12 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
            <Text variant="heading">Habilidades</Text>
            <Button label="Editar" variant="ghost" size="sm" icon="create-outline" onPress={() => router.push('/edit-profile')} />
          </View>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
            {p.skills.slice(0, SKILLS_PREVIEW).map((s) => (
              <Badge key={s.key || s.name} label={`${s.name} · ${skillLevelLabel[s.level].toLowerCase()}`} tone={s.level === 'advanced' ? 'brand' : 'neutral'} />
            ))}
            {p.skills.length > SKILLS_PREVIEW ? <Badge label={`+${p.skills.length - SKILLS_PREVIEW} más`} tone="neutral" /> : null}
          </View>
        </Card>
      ) : null}

      {cv ? (
        <ListGroup title="Tu CV">
          <ListRow icon="refresh" title="Reemplazar CV" subtitle="Subir una versión más reciente" onPress={uploadCv} testID="cv-replace" />
          <ListRow icon="trash-outline" title="Eliminar CV" subtitle="Borramos el archivo y el texto extraído" tone="danger" onPress={() => setConfirmCv(true)} testID="cv-delete" />
        </ListGroup>
      ) : null}

      <View style={{ gap: 8 }}>
        <Text variant="label" tone="subtle" style={{ paddingHorizontal: 4 }}>Apariencia</Text>
        <SegmentedTabs<AppearancePreference>
          value={appearance}
          onChange={setAppearance}
          options={[
            { value: 'system', label: 'Automático' },
            { value: 'light', label: 'Claro' },
            { value: 'dark', label: 'Oscuro' },
          ]}
        />
      </View>

      {pr ? (
        <ListGroup title="Avisos">
          <ListRow icon="notifications-outline" title="Frecuencia de avisos" subtitle={`${frequencyLabel[pr.notificationFrequency]}${pr.pushEnabled ? ' · en el celular' : ''}`} onPress={() => router.push('/edit-preferences')} />
        </ListGroup>
      ) : null}

      <ListGroup title="Sobre ChambaIA">
        <ListRow icon="globe-outline" title="¿De dónde salen las ofertas?" subtitle="Fuentes, frescura y cómo evitamos duplicados" onPress={() => setSourcesOpen(true)} testID="sources-row" />
      </ListGroup>

      <ListGroup title="Cuenta">
        <ListRow icon="log-out-outline" title="Cerrar sesión" onPress={() => setConfirmLogout(true)} testID="logout" />
        <ListRow icon="time-outline" title="Borrar mi historial" subtitle="Guardadas, descartadas y postulaciones" onPress={() => setConfirmHistory(true)} testID="clear-history" />
        <ListRow icon="warning-outline" title="Eliminar mi cuenta y mis datos" tone="danger" onPress={() => { setPassword(''); setDeleteOpen(true); }} testID="delete-account" />
      </ListGroup>

      <Text variant="caption" tone="subtle" style={{ textAlign: 'center', paddingVertical: 8 }}>ChambaIA · Hecho en Perú 🇵🇪</Text>

      <ConfirmDialog
        visible={confirmLogout}
        icon="log-out-outline"
        title="¿Cerrar sesión?"
        message="Tu agente seguirá buscando; solo saldrás de este dispositivo."
        confirmLabel="Cerrar sesión"
        onCancel={() => setConfirmLogout(false)}
        onConfirm={() => { setConfirmLogout(false); void logout(); }}
      />

      <ConfirmDialog
        visible={confirmCv}
        title="¿Eliminar tu CV?"
        message="Borramos el archivo y el texto que extrajimos. Tu perfil y tus preferencias se quedan como están."
        confirmLabel="Eliminar CV"
        destructive
        onCancel={() => setConfirmCv(false)}
        onConfirm={() => {
          setConfirmCv(false);
          if (cv) deleteResume.mutate(cv.id, { onSuccess: () => { haptics.warning(); toast.show({ message: 'Eliminamos tu CV' }); } });
        }}
      />

      <ConfirmDialog
        visible={confirmHistory}
        title="¿Borrar tu historial?"
        message="Olvidamos lo que guardaste, descartaste o postulaste, y tu tablero de postulaciones. Tu perfil y tus preferencias no cambian."
        confirmLabel="Borrar historial"
        destructive
        onCancel={() => setConfirmHistory(false)}
        onConfirm={() => {
          setConfirmHistory(false);
          clearHistory.mutate(undefined, { onSuccess: () => { haptics.warning(); toast.show({ message: 'Borramos tu historial' }); } });
        }}
      />

      <SourcesSheet visible={sourcesOpen} onClose={() => setSourcesOpen(false)} />

      <BottomSheet visible={deleteOpen} onClose={() => setDeleteOpen(false)} title="Eliminar mi cuenta">
        <Text tone="muted">Borraremos para siempre tu perfil, preferencias, postulaciones y todo lo que tu agente aprendió de ti. No se puede deshacer.</Text>
        <Input label="Confirma con tu contraseña" value={password} onChangeText={setPassword} secureTextEntry icon="lock-closed-outline" autoComplete="current-password" />
        {deleteAccount.error ? <InlineError message={deleteAccount.error.message} /> : null}
        <Button label="Eliminar definitivamente" variant="danger" loading={deleteAccount.isPending} disabled={password.length === 0} onPress={() => deleteAccount.mutate(password, { onError: () => haptics.error() })} fullWidth />
      </BottomSheet>
    </Screen>
  );
}
