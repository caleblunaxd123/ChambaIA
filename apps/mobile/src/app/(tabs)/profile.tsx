import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { useClearHistory, useDeleteResume, usePreferences, useProfile, useResumes } from '@/api/queries';
import { logout, useDeleteAccount } from '@/features/auth/useAuthActions';
import { educationLabel, firstName, formatDuration, formatRelativeTime, formatMoney, frequencyLabel, modalityLabel, skillLevelLabel } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { useAuthStore } from '@/state/auth-store';
import { Badge } from '@/ui/Badge';
import { BottomSheet } from '@/ui/BottomSheet';
import { Button } from '@/ui/Button';
import { Card } from '@/ui/Card';
import { ConfirmDialog } from '@/ui/ConfirmDialog';
import { ErrorState } from '@/ui/EmptyState';
import { Icon, type IconName } from '@/ui/Icon';
import { Input } from '@/ui/Input';
import { Screen } from '@/ui/Screen';
import { Skeleton } from '@/ui/Skeleton';
import { Text } from '@/ui/Text';
import { useTheme } from '@/ui/theme';

export default function ProfileScreen() {
  const router = useRouter();
  const { colors } = useTheme();
  const user = useAuthStore((s) => s.user);
  const profile = useProfile();
  const prefs = usePreferences();
  const [confirmLogout, setConfirmLogout] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [password, setPassword] = useState('');
  const deleteAccount = useDeleteAccount();
  const resumes = useResumes();
  const deleteResume = useDeleteResume();
  const clearHistory = useClearHistory();
  const [confirmCv, setConfirmCv] = useState(false);
  const [confirmHistory, setConfirmHistory] = useState(false);
  const cv = resumes.data?.[0];

  const p = profile.data;
  const pr = prefs.data;
  const education = p?.educationLevel ? `${educationLabel[p.educationLevel]}${p.educationStatus === 'inProgress' ? ' (en curso)' : p.educationStatus === 'completed' ? ' (terminado)' : ''}` : null;

  return (
    <Screen onRefresh={() => { void profile.refetch(); void prefs.refetch(); }} refreshing={profile.isRefetching}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: 14 }}>
        <View style={{ width: 64, height: 64, borderRadius: 32, backgroundColor: colors.accentTint, alignItems: 'center', justifyContent: 'center' }}>
          <Text variant="title" style={{ color: colors.warning }}>{firstName(user?.fullName).charAt(0).toUpperCase()}</Text>
        </View>
        <View style={{ flex: 1, gap: 2 }}>
          <Text variant="title" numberOfLines={1} testID="profile-name">{user?.fullName}</Text>
          <Text variant="caption" tone="muted" numberOfLines={1}>{user?.email}</Text>
          <View style={{ marginTop: 4 }}><Badge label="Plan Gratis" tone="brand" icon="sparkles" size="sm" /></View>
        </View>
      </View>

      <Card style={{ gap: 12 }} testID="cv-card">
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <Icon name="document-text-outline" size={20} tone="primary" />
          <Text variant="heading">Tu CV</Text>
        </View>
        {resumes.isLoading ? (
          <Skeleton width="60%" />
        ) : cv ? (
          <>
            <View style={{ gap: 2 }}>
              <Text variant="bodyStrong" numberOfLines={1} testID="cv-name">{cv.originalFilename}</Text>
              <Text variant="caption" tone="muted">Subido {formatRelativeTime(cv.createdAt)} · {(cv.sizeBytes / 1024).toFixed(0)} KB</Text>
            </View>
            <View style={{ flexDirection: 'row', gap: 8 }}>
              <Button label="Reemplazar" size="sm" variant="secondary" icon="refresh" onPress={() => router.push({ pathname: '/onboarding', params: { mode: 'cv' } })} testID="cv-replace" />
              <Button label="Eliminar" size="sm" variant="danger" icon="trash-outline" onPress={() => setConfirmCv(true)} testID="cv-delete" />
            </View>
          </>
        ) : (
          <>
            <Text tone="muted">Sube tu CV una sola vez y completamos tu perfil por ti. Tú revisas todo antes de guardarlo.</Text>
            <Button label="Subir mi CV" icon="cloud-upload-outline" onPress={() => router.push({ pathname: '/onboarding', params: { mode: 'cv' } })} testID="cv-upload" />
          </>
        )}
      </Card>

      {profile.isError ? (
        <ErrorState message={profile.error.message} onRetry={() => void profile.refetch()} />
      ) : (
        <Card style={{ gap: 14 }} testID="profile-card">
          <Header title="Mi perfil" action="Editar" onAction={() => router.push('/edit-profile')} />
          {profile.isLoading || !p ? (
            <View style={{ gap: 10 }}><Skeleton width="80%" /><Skeleton width="60%" /><Skeleton width="70%" /></View>
          ) : (
            <>
              {p.headline ? <Text>{p.headline}</Text> : <Text tone="subtle">Aún sin titular profesional.</Text>}
              <Fact icon="briefcase-outline" text={p.experienceMonths > 0 ? `${formatDuration(p.experienceMonths)} de experiencia` : 'Experiencia sin completar'} />
              <Fact icon="school-outline" text={education ?? 'Estudios sin completar'} />
              {p.skills.length > 0 ? (
                <View style={{ gap: 8 }}>
                  <Text variant="label" tone="subtle">Habilidades</Text>
                  <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 8 }}>
                    {p.skills.map((s) => <Badge key={s.key} label={`${s.name} · ${skillLevelLabel[s.level].toLowerCase()}`} tone="neutral" />)}
                  </View>
                </View>
              ) : (
                <Button label="Agregar mis habilidades" icon="add" variant="secondary" size="sm" onPress={() => router.push('/edit-profile')} />
              )}
            </>
          )}
        </Card>
      )}

      <Card style={{ gap: 12 }} testID="preferences-card">
        <Header title="Lo que busco" action="Editar" onAction={() => router.push('/edit-preferences')} />
        {prefs.isLoading || !pr ? (
          <View style={{ gap: 10 }}><Skeleton width="70%" /><Skeleton width="55%" /></View>
        ) : (
          <>
            <Fact icon="cash-outline" text={pr.minSalary != null ? `Sueldo mínimo ${formatMoney(pr.minSalary)}` : 'Sin sueldo mínimo'} />
            <Fact icon="location-outline" text={pr.homeDistrict ? `Vivo en ${pr.homeDistrict}${pr.maxCommuteMinutes ? ` · viaje máx. ${pr.maxCommuteMinutes} min` : ''}` : 'Distrito sin indicar'} />
            <Fact icon="business-outline" text={pr.preferredModalities.length > 0 ? pr.preferredModalities.map((m) => modalityLabel[m]).join(', ') : 'Cualquier modalidad'} />
            <Fact icon="notifications-outline" text={`Avisos: ${frequencyLabel[pr.notificationFrequency].toLowerCase()}`} />
          </>
        )}
      </Card>

      <View style={{ gap: 10 }}>
        <Button label="Cerrar sesión" variant="secondary" icon="log-out-outline" onPress={() => setConfirmLogout(true)} fullWidth testID="logout" />
        <Button label="Borrar mi historial de búsqueda" variant="ghost" onPress={() => setConfirmHistory(true)} fullWidth testID="clear-history" />
        <Button label="Eliminar mi cuenta y mis datos" variant="ghost" onPress={() => { setPassword(''); setDeleteOpen(true); }} fullWidth testID="delete-account" />
      </View>

      <ConfirmDialog
        visible={confirmLogout}
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
          if (cv) deleteResume.mutate(cv.id, { onSuccess: () => haptics.warning() });
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
          clearHistory.mutate(undefined, { onSuccess: () => haptics.warning() });
        }}
      />

      <BottomSheet visible={deleteOpen} onClose={() => setDeleteOpen(false)} title="Eliminar mi cuenta">
        <Text tone="muted">Borraremos para siempre tu perfil, preferencias, postulaciones y todo lo que tu agente aprendió de ti. No se puede deshacer.</Text>
        <Input label="Confirma con tu contraseña" value={password} onChangeText={setPassword} secureTextEntry icon="lock-closed-outline" autoComplete="current-password" />
        {deleteAccount.error ? <Text tone="danger">{deleteAccount.error.message}</Text> : null}
        <Button label="Eliminar definitivamente" variant="danger" loading={deleteAccount.isPending} disabled={password.length === 0} onPress={() => deleteAccount.mutate(password, { onError: () => haptics.error() })} fullWidth />
      </BottomSheet>
    </Screen>
  );
}

function Header({ title, action, onAction }: { title: string; action: string; onAction: () => void }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
      <Text variant="heading">{title}</Text>
      <Button label={action} variant="ghost" size="sm" icon="create-outline" onPress={onAction} />
    </View>
  );
}

function Fact({ icon, text }: { icon: IconName; text: string }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 10 }}>
      <Icon name={icon} size={18} tone="muted" />
      <Text style={{ flex: 1 }}>{text}</Text>
    </View>
  );
}
